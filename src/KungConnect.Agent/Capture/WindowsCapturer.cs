using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Capture;

/// <summary>
/// Windows screen capture using DXGI Desktop Duplication API.
/// Requires Windows 8+ and runs on the GPU-owning thread.
/// TODO: Phase 2 – implement via SharpDX.DXGI or Windows.Graphics.Capture WinRT API.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsCapturer(ILogger<WindowsCapturer> logger) : IScreenCapturer
{
    public int MonitorCount { get; private set; } = 1;
    public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;

    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    public Task StartAsync(int monitorIndex = 0, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _captureTask = CaptureLoopAsync(monitorIndex, _cts.Token);
        logger.LogInformation("Windows DXGI capture started on monitor {Index}", monitorIndex);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_captureTask is not null)
            await _captureTask.ConfigureAwait(false);
        logger.LogInformation("Windows DXGI capture stopped");
    }

    private async Task CaptureLoopAsync(int monitorIndex, CancellationToken ct)
    {
        // TODO Phase 2: Replace stub with real DXGI Desktop Duplication
        // using SharpDX.DXGI or PInvoke to IDXGIOutputDuplication.AcquireNextFrame
        logger.LogWarning("WindowsCapturer: capture loop is a stub – implement DXGI in Phase 2");
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000 / 30, ct); // 30 fps placeholder
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
