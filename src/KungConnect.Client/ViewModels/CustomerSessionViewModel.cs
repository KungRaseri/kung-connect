using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KungConnect.Client.Services;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// Displayed on the customer's machine when launched via
/// <c>kungconnect://join?code=...&amp;server=...</c>.
///
/// The customer's app connects to the server SignalR hub and waits for the
/// support operator to begin the session.  Screen capture / remote-input
/// acceptance is a Phase 2 concern; this view handles the connection state
/// and gives the customer a way to cancel.
/// </summary>
public sealed partial class CustomerSessionViewModel : ViewModelBase
{
    private readonly ISignalingService _signaling;
    private readonly string _joinCode;
    private CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _connectionStatus = "Connecting…";

    public string JoinCode => _joinCode;

    public event EventHandler? SessionEnded;

    public CustomerSessionViewModel(string joinCode, ISignalingService signaling)
    {
        _joinCode  = joinCode;
        _signaling = signaling;
    }

    /// <summary>
    /// Called after the window is shown.  Connects to the server SignalR hub
    /// and waits for the operator to initiate the session.
    /// </summary>
    public async Task ConnectAsync()
    {
        try
        {
            ConnectionStatus = "Connecting to server\u2026";
            // Customer connects without a JWT — the server hub accepts the join code
            // via the anonymous AgentRegister / JoinCodeConnect path.
            // TODO Phase 2: await _signaling.ConnectAsCustomerAsync(_joinCode, _cts.Token);
            await Task.CompletedTask;
            ConnectionStatus = "Waiting for support agent to connect\u2026";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection failed: {ex.Message}";
            ConnectionStatus = "Disconnected";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _cts.CancelAsync();
        if (_signaling.IsConnected)
            await _signaling.DisconnectAsync();
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
    }
}
