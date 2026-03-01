using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KungConnect.Client.Services;
using KungConnect.Shared.DTOs.Machines;
using KungConnect.Shared.DTOs.Sessions;
using KungConnect.Shared.Enums;
using KungConnect.Shared.Signaling;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// ViewModel for the active remote session screen.
/// Manages WebRTC signaling lifecycle and exposes the decoded remote frame as a Bitmap.
/// Input events are forwarded back over the WebRTC data channel (via a future
/// RTCPeerConnection in SessionHandlerClientService — placeholder for Phase 2).
/// </summary>
public sealed partial class SessionViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ISignalingService _signaling;
    private readonly ISessionService _sessionService;
    private readonly MachineDto _machine;
    private Guid _sessionId;
    private CancellationTokenSource _cts = new();

    [ObservableProperty]
    private Bitmap? _remoteFrame;

    [ObservableProperty]
    private string _remoteAlias = string.Empty;

    [ObservableProperty]
    private SessionState _state = SessionState.Pending;

    [ObservableProperty]
    private double _remoteWidth = 1280;

    [ObservableProperty]
    private double _remoteHeight = 720;

    public event EventHandler? SessionEnded;

    public SessionViewModel(
        ISignalingService signaling,
        ISessionService sessionService,
        MachineDto machine)
    {
        _signaling = signaling;
        _sessionService = sessionService;
        _machine = machine;
        RemoteAlias = machine.Alias;

        _signaling.AnswerReceived += OnAnswerReceived;
        _signaling.IceCandidateReceived += OnIceCandidateReceived;
        _signaling.SessionStateChanged += OnSessionStateChanged;
    }

    // Called by MachineListView when navigating to the session screen
    public async Task StartAsync()
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var dto = await _sessionService.RequestSessionAsync(
                new RequestSessionDto(_machine.Id), _cts.Token);
            _sessionId = dto.Id;
            State = dto.State;
            StatusMessage = "Waiting for agent approval…";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Session request failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    // Receives decoded JPEG frame bytes forwarded by the RTCPeerConnection data channel
    // (Phase 2: the actual WebRTC decode pipeline posts here via a callback)
    public void ReceiveFrame(byte[] jpegBytes)
    {
        using var ms = new System.IO.MemoryStream(jpegBytes);
        var bmp = new Bitmap(ms);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => RemoteFrame = bmp);
    }

    private void OnAnswerReceived(object? sender, SdpMessage sdp)
    {
        // Phase 2: feed sdp.Sdp into RTCPeerConnection.SetRemoteDescriptionAsync
        StatusMessage = "Remote SDP answer received — establishing connection…";
    }

    private void OnIceCandidateReceived(object? sender, IceCandidateMessage ice)
    {
        // Phase 2: feed ice into RTCPeerConnection.AddIceCandidate
    }

    private void OnSessionStateChanged(object? sender, SessionStateMessage msg)
    {
        if (msg.SessionId != _sessionId) return;

        if (!Enum.TryParse<SessionState>(msg.State, ignoreCase: true, out var newState)) return;
        State = newState;

        switch (newState)
        {
            case SessionState.Active:
                StatusMessage = "Connected.";
                break;
            case SessionState.Rejected:
                ErrorMessage = "Session was rejected by the agent.";
                SessionEnded?.Invoke(this, EventArgs.Empty);
                break;
            case SessionState.Terminated:
                StatusMessage = "Session ended.";
                SessionEnded?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            await _signaling.EndSessionAsync(_sessionId, _cts.Token);
        }
        catch { /* best-effort */ }
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        _signaling.AnswerReceived -= OnAnswerReceived;
        _signaling.IceCandidateReceived -= OnIceCandidateReceived;
        _signaling.SessionStateChanged -= OnSessionStateChanged;

        await _cts.CancelAsync();
        _cts.Dispose();
        RemoteFrame?.Dispose();
    }
}
