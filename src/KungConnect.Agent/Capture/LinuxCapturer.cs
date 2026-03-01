using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Capture;

/// <summary>
/// Linux screen capture via XShm (X11) or PipeWire portal.
/// TODO: Phase 2 – implement via libxcb XShm or PipeWire screencopy portal.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxCapturer(ILogger<LinuxCapturer> logger) : IScreenCapturer
{
    public int MonitorCount { get; private set; } = 1;
    public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;

    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    public Task StartAsync(int monitorIndex = 0, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _captureTask = CaptureLoopAsync(monitorIndex, _cts.Token);
        logger.LogInformation("Linux XShm/PipeWire capture started on monitor {Index}", monitorIndex);
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
        // TODO Phase 2: P/Invoke libxcb / PipeWire
        logger.LogWarning("LinuxCapturer: capture loop is a stub – implement XShm/PipeWire in Phase 2");
        while (!ct.IsCancellationRequested)
            await Task.Delay(1000 / 30, ct);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
