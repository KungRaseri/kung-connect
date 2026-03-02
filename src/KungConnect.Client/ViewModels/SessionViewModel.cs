using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KungConnect.Client.Services;
using KungConnect.Shared.DTOs.Machines;
using KungConnect.Shared.DTOs.Sessions;
using KungConnect.Shared.Enums;
using KungConnect.Shared.Signaling;
using SIPSorcery.Net;
using System.IO;
using System.Text.Json;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// ViewModel for the active remote session screen.
///
/// Supports two modes:
///   • Machine session  – agent creates RTCPeerConnection first, sends SDP offer to operator.
///                        Operator receives offer (OfferReceived), creates answer, sends back.
///   • Ad-hoc session   – operator creates RTCPeerConnection, sends offer to browser customer.
///                        Browser creates answer; operator receives it (AnswerReceived).
///
/// Video flows as JPEG frames over a "video" data channel.
/// Input events are sent by the operator over an "input" data channel.
/// </summary>
public sealed partial class SessionViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ISignalingService _signaling;
    private readonly ISessionService _sessionService;
    private readonly MachineDto _machine;
    private Guid _sessionId;
    private CancellationTokenSource _cts = new();

    // ── WebRTC state ─────────────────────────────────────────────────────────
    private RTCPeerConnection? _pc;
    private RTCDataChannel? _inputDataChannel;   // operator writes input events to the agent/browser

    [ObservableProperty] private Bitmap? _remoteFrame;
    [ObservableProperty] private string  _remoteAlias = string.Empty;
    [ObservableProperty] private SessionState _state = SessionState.Pending;
    [ObservableProperty] private double _remoteWidth  = 1280;
    [ObservableProperty] private double _remoteHeight = 720;

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

        _signaling.OfferReceived        += OnOfferReceived;
        _signaling.AnswerReceived        += OnAnswerReceived;
        _signaling.IceCandidateReceived  += OnIceCandidateReceived;
        _signaling.SessionStateChanged   += OnSessionStateChanged;
        _signaling.SessionApproved       += OnSessionApproved;
    }

    // ── Session start ─────────────────────────────────────────────────────────

    /// <summary>
    /// Machine session: create the DB record, then ask the hub to notify the agent.
    /// Once the agent approves, it sends an SDP offer → <see cref="OnOfferReceived"/>.
    /// </summary>
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

            // Notify agent via hub
            await _signaling.RequestSessionAsync(_sessionId, _machine.Id, _cts.Token);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Session request failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Ad-hoc session: operator creates the offer (browser customer is the answerer).
    /// Data channels are opened by the operator (offerer) and received by the browser via ondatachannel.
    /// </summary>
    public async Task StartAdHocAsync(string targetConnectionId)
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            _sessionId = Guid.NewGuid(); // local tracking id for ad-hoc
            _pc = BuildPeerConnection();

            // ── Data channels (operator is offerer, so operator creates them) ─
            var videoDc = await _pc.createDataChannel("video",
                new RTCDataChannelInit { ordered = false, maxRetransmits = 0 });
            var inputDc = await _pc.createDataChannel("input",
                new RTCDataChannelInit { ordered = true });

            // "video" channel: browser sends JPEG frames, we receive and display them
            videoDc.onmessage += (_, _, data) => ReceiveFrame(data);
            _inputDataChannel = inputDc;

            // ICE from our side → agent/browser
            _pc.onicecandidate += cand =>
            {
                if (cand is null) return;
                _ = _signaling.SendIceCandidateAsync(targetConnectionId, _sessionId,
                    cand.candidate, cand.sdpMid ?? "", cand.sdpMLineIndex, _cts.Token);
            };

            _pc.onconnectionstatechange += s =>
                Dispatcher.UIThread.Post(() => StatusMessage = $"WebRTC: {s}");

            // Create offer (includes both data channels)
            var offer = _pc.createOffer();
            await _pc.setLocalDescription(offer);

            await _signaling.SendOfferAsync(targetConnectionId, _sessionId, offer.sdp, _cts.Token);

            State = SessionState.Active;
            StatusMessage = "Offer sent — waiting for browser customer…";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ad hoc session failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    // ── Input forwarding ──────────────────────────────────────────────────────

    /// <summary>Send an input event to the remote agent or browser customer.</summary>
    public void SendInput(InputEvent evt)
    {
        if (_inputDataChannel is null) return;
        if (_inputDataChannel.readyState != RTCDataChannelState.open) return;
        _inputDataChannel.send(JsonSerializer.Serialize(evt));
    }

    // ── Receive JPEG video frame from remote ──────────────────────────────────

    public void ReceiveFrame(byte[] jpegBytes)
    {
        try
        {
            using var ms = new MemoryStream(jpegBytes);
            var bmp = new Bitmap(ms);
            Dispatcher.UIThread.Post(() =>
            {
                var old = RemoteFrame;
                RemoteFrame = bmp;
                old?.Dispose();
            });
        }
        catch { /* malformed frame — skip */ }
    }

    // ── Signaling event handlers ──────────────────────────────────────────────

    private void OnSessionApproved(object? sender, Guid sessionId)
    {
        if (sessionId != _sessionId) return;
        StatusMessage = "Agent approved — waiting for WebRTC offer…";
        State = SessionState.Approved;
    }

    private async void OnOfferReceived(object? sender, SdpMessage sdp)
    {
        // This fires for machine sessions: agent sent us their offer.
        _sessionId = sdp.SessionId;
        var agentConnId = sdp.SenderConnectionId;

        _pc = BuildPeerConnection();

        // Receive data channels created by the agent (offerer)
        _pc.ondatachannel += dc =>
        {
            if (dc.label == "video")
                dc.onmessage += (_, _, data) => ReceiveFrame(data);
            else if (dc.label == "input")
                _inputDataChannel = dc;
        };

        _pc.onicecandidate += cand =>
        {
            if (cand is null) return;
            _ = _signaling.SendIceCandidateAsync(agentConnId, _sessionId,
                cand.candidate, cand.sdpMid ?? "", cand.sdpMLineIndex, _cts.Token);
        };

        _pc.onconnectionstatechange += s =>
            Dispatcher.UIThread.Post(() => StatusMessage = $"WebRTC: {s}");

        try
        {
            // Set remote description (agent's offer)
            var setResult = _pc.setRemoteDescription(
                new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp.Sdp });

            if (setResult != SetDescriptionResultEnum.OK)
            {
                ErrorMessage = $"setRemoteDescription failed: {setResult}";
                return;
            }

            // Create and send answer
            var answer = _pc.createAnswer();
            await _pc.setLocalDescription(answer);

            await _signaling.SendAnswerAsync(agentConnId, _sessionId, answer.sdp, _cts.Token);
            Dispatcher.UIThread.Post(() => StatusMessage = "Answer sent — ICE negotiating…");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"WebRTC setup failed: {ex.Message}";
        }
    }

    private void OnAnswerReceived(object? sender, SdpMessage sdp)
    {
        // Ad-hoc: browser customer sent the SDP answer.
        if (_pc is null) return;
        try
        {
            var result = _pc.setRemoteDescription(
                new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp.Sdp });

            if (result != SetDescriptionResultEnum.OK)
                ErrorMessage = $"setRemoteDescription(answer) failed: {result}";
            else
                Dispatcher.UIThread.Post(() => StatusMessage = "ICE negotiating…");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to set answer: {ex.Message}";
        }
    }

    private void OnIceCandidateReceived(object? sender, IceCandidateMessage ice)
    {
        if (_pc is null) return;
        try
        {
            _pc.addIceCandidate(new RTCIceCandidateInit
            {
                candidate     = ice.Candidate,
                sdpMid        = ice.SdpMid,
                sdpMLineIndex = (ushort)(ice.SdpMLineIndex ?? 0)
            });
        }
        catch { /* non-fatal — ICE negotiation can continue */ }
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private RTCPeerConnection BuildPeerConnection() =>
        new(new RTCConfiguration
        {
            iceServers = [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }]
        });

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try { await _signaling.EndSessionAsync(_sessionId, _cts.Token); }
        catch { /* best-effort */ }
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        _signaling.OfferReceived       -= OnOfferReceived;
        _signaling.AnswerReceived       -= OnAnswerReceived;
        _signaling.IceCandidateReceived -= OnIceCandidateReceived;
        _signaling.SessionStateChanged  -= OnSessionStateChanged;
        _signaling.SessionApproved      -= OnSessionApproved;

        await _cts.CancelAsync();
        _cts.Dispose();

        _pc?.close();
        _pc = null;
        RemoteFrame?.Dispose();
    }
}
