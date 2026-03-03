namespace KungConnect.Agent;

public enum AgentState { Starting, Connecting, Connected, Disconnected }

/// <summary>
/// Thread-safe status object shared between <see cref="Worker"/> and the system tray.
/// Worker writes to it from a thread-pool thread; the tray polls it on the UI thread.
/// </summary>
public sealed class AgentConnectionStatus
{
    private volatile int _state = (int)AgentState.Starting;

    public AgentState State
    {
        get => (AgentState)_state;
        set => Interlocked.Exchange(ref _state, (int)value);
    }
}
