using KungConnect.Shared.Enums;

namespace KungConnect.Shared.Interfaces;

/// <summary>
/// Abstracts OS-specific screen capture.  Implementations exist for
/// Windows (DXGI), macOS (CoreGraphics), and Linux (XShm / PipeWire).
/// </summary>
public interface IScreenCapturer : IDisposable
{
    /// <summary>Number of available monitors (populated after Start).</summary>
    int MonitorCount { get; }

    /// <summary>Target capture frame rate. Set before calling <see cref="StartAsync"/>.</summary>
    int TargetFps { get; set; }

    /// <summary>Fires whenever a new raw frame is available.</summary>
    event EventHandler<FrameCapturedEventArgs>? FrameCaptured;

    Task StartAsync(int monitorIndex = 0, CancellationToken cancellationToken = default);
    Task StopAsync();
}

public sealed class FrameCapturedEventArgs : EventArgs
{
    public required byte[] Data { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    /// <summary>Raw pixel format, e.g. "BGRA32".</summary>
    public string Format { get; init; } = "BGRA32";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
