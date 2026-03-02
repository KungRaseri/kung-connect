using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Capture;

/// <summary>
/// Windows screen capture using GDI BitBlt (compatible with Windows 7+).
/// Frames are captured as BGRA bitmaps and JPEG-encoded via SixLabors.ImageSharp.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsCapturer(ILogger<WindowsCapturer> logger) : IScreenCapturer
{
    public int MonitorCount { get; private set; } = 1;
    public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;

    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    // ── Win32 GDI P/Invoke ─────────────────────────────────────────────────
    [DllImport("gdi32.dll")] static extern bool     BitBlt(nint hdcDest, int x, int y, int w, int h, nint hdcSrc, int xSrc, int ySrc, uint rop);
    [DllImport("gdi32.dll")] static extern nint     CreateCompatibleDC(nint hdc);
    [DllImport("gdi32.dll")] static extern nint     CreateCompatibleBitmap(nint hdc, int w, int h);
    [DllImport("gdi32.dll")] static extern nint     SelectObject(nint hdc, nint h);
    [DllImport("gdi32.dll")] static extern bool     DeleteObject(nint ho);
    [DllImport("gdi32.dll")] static extern bool     DeleteDC(nint hdc);
    [DllImport("gdi32.dll")] static extern int      GetDIBits(nint hdc, nint hbm, uint start, uint lines, byte[]? bits, ref BITMAPINFO bmi, uint usage);
    [DllImport("user32.dll")] static extern nint    GetDC(nint hwnd);
    [DllImport("user32.dll")] static extern int     ReleaseDC(nint hwnd, nint hdc);
    [DllImport("user32.dll")] static extern int     GetSystemMetrics(int nIndex);

    private const uint SRCCOPY = 0x00CC0020;
    private const int  SM_CXSCREEN = 0;
    private const int  SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint   biSize;
        public int    biWidth;
        public int    biHeight;   // negative = top-down
        public ushort biPlanes;
        public ushort biBitCount;
        public uint   biCompression;
        public uint   biSizeImage;
        public int    biXPelsPerMeter;
        public int    biYPelsPerMeter;
        public uint   biClrUsed;
        public uint   biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint             bmiColors;  // unused for 32-bit
    }

    private static readonly JpegEncoder _jpegEncoder = new() { Quality = 60 };

    public Task StartAsync(int monitorIndex = 0, CancellationToken cancellationToken = default)
    {
        _cts         = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _captureTask = CaptureLoopAsync(monitorIndex, _cts.Token);
        logger.LogInformation("Windows GDI capture started on monitor {Index}", monitorIndex);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_captureTask is not null)
            await _captureTask.ConfigureAwait(false);
        logger.LogInformation("Windows GDI capture stopped");
    }

    private async Task CaptureLoopAsync(int monitorIndex, CancellationToken ct)
    {
        const int TargetFps = 10;
        const int IntervalMs = 1000 / TargetFps;

        while (!ct.IsCancellationRequested)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var frame = CaptureFrame();
                if (frame is not null)
                    FrameCaptured?.Invoke(this, frame);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Frame capture failed");
            }

            var elapsed = (int)sw.ElapsedMilliseconds;
            var delay   = Math.Max(1, IntervalMs - elapsed);
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private FrameCapturedEventArgs? CaptureFrame()
    {
        var screenW = GetSystemMetrics(SM_CXSCREEN);
        var screenH = GetSystemMetrics(SM_CYSCREEN);
        if (screenW <= 0 || screenH <= 0) return null;

        var hdcScreen = GetDC(nint.Zero);
        var hdcMem    = CreateCompatibleDC(hdcScreen);
        var hBitmap   = CreateCompatibleBitmap(hdcScreen, screenW, screenH);
        var hOld      = SelectObject(hdcMem, hBitmap);

        try
        {
            BitBlt(hdcMem, 0, 0, screenW, screenH, hdcScreen, 0, 0, SRCCOPY);

            var bgraData = new byte[screenW * screenH * 4];
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize        = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth       = screenW,
                    biHeight      = -screenH,  // negative = top-down scan
                    biPlanes      = 1,
                    biBitCount    = 32,
                    biCompression = 0           // BI_RGB
                }
            };
            GetDIBits(hdcMem, hBitmap, 0, (uint)screenH, bgraData, ref bmi, 0);

            var jpegBytes = EncodeBgraToJpeg(bgraData, screenW, screenH);
            return new FrameCapturedEventArgs
            {
                Data   = jpegBytes,
                Width  = screenW,
                Height = screenH,
                Format = "JPEG"
            };
        }
        finally
        {
            SelectObject(hdcMem, hOld);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(nint.Zero, hdcScreen);
        }
    }

    private static byte[] EncodeBgraToJpeg(byte[] bgra, int width, int height)
    {
        using var image = Image.LoadPixelData<Bgra32>(bgra, width, height);
        using var ms    = new MemoryStream();
        image.SaveAsJpeg(ms, _jpegEncoder);
        return ms.ToArray();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

