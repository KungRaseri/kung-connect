using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Capture;

/// <summary>
/// macOS screen capture using CoreGraphics CGDisplayCreateImage via P/Invoke.
/// TODO: Phase 2 – implement P/Invoke to CoreGraphics / ScreenCaptureKit.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOsCapturer(ILogger<MacOsCapturer> logger) : IScreenCapturer
{
    public int MonitorCount { get; private set; } = 1;
    public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;

    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    public Task StartAsync(int monitorIndex = 0, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _captureTask = CaptureLoopAsync(monitorIndex, _cts.Token);
        logger.LogInformation("macOS CoreGraphics capture started on monitor {Index}", monitorIndex);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_captureTask is not null)
            await _captureTask.ConfigureAwait(false);
    }

    private async Task CaptureLoopAsync(int monitorIndex, CancellationToken ct)
    {
        // TODO Phase 2: P/Invoke CGDisplayCreateImage / ScreenCaptureKit
        logger.LogWarning("MacOsCapturer: capture loop is a stub – implement CoreGraphics in Phase 2");
        while (!ct.IsCancellationRequested)
            await Task.Delay(1000 / 30, ct);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
