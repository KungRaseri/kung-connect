namespace KungConnect.Agent;

public enum AgentState { Starting, Connecting, Connected, Disconnected }

/// <summary>
/// Thread-safe status object shared between <see cref="Worker"/>,
/// <see cref="Services.UpdateCheckerService"/>, and the system tray.
/// Worker writes State from a thread-pool thread; the tray polls on the UI thread.
/// </summary>
public sealed class AgentConnectionStatus
{
    private volatile int    _state = (int)AgentState.Starting;
    private          string? _updateAvailableVersion;
    private          string? _updateAvailableUrl;

    public AgentState State
    {
        get => (AgentState)_state;
        set => Interlocked.Exchange(ref _state, (int)value);
    }

    /// <summary>Set by UpdateCheckerService when a newer release is found.</summary>
    public string? UpdateAvailableVersion
    {
        get => Volatile.Read(ref _updateAvailableVersion);
        set => Volatile.Write(ref _updateAvailableVersion, value);
    }

    /// <summary>Browser URL for the GitHub release page.</summary>
    public string? UpdateAvailableUrl
    {
        get => Volatile.Read(ref _updateAvailableUrl);
        set => Volatile.Write(ref _updateAvailableUrl, value);
    }
}
