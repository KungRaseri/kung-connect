using Microsoft.AspNetCore.SignalR.Client;
using KungConnect.Shared.Constants;
using KungConnect.Shared.Signaling;

namespace KungConnect.Client.Services;

public sealed class SignalingService : ISignalingService
{
    private HubConnection? _hub;
    private readonly string _hubUrl;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event EventHandler<SdpMessage>? OfferReceived;
    public event EventHandler<SdpMessage>? AnswerReceived;
    public event EventHandler<IceCandidateMessage>? IceCandidateReceived;
    public event EventHandler<SessionStateMessage>? SessionStateChanged;
    public event EventHandler<Guid>? SessionApproved;
    public event EventHandler<Guid>? SessionRejected;

    public SignalingService(IHttpClientFactory factory)
    {
        var client = factory.CreateClient("KungConnect");
        _hubUrl = new Uri(client.BaseAddress!, "/hubs/signaling").ToString();
    }

    public async Task ConnectAsync(string accessToken, CancellationToken ct = default)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .Build();

        // Agent sends offer to operator (machine sessions)
        _hub.On<Guid, string, string>(SignalingEvents.ReceiveOffer, (sessionId, sdp, senderConnId) =>
            OfferReceived?.Invoke(this, new SdpMessage { Type = "offer", SessionId = sessionId, Sdp = sdp, SenderConnectionId = senderConnId }));

        // Browser customer sends answer to operator (ad-hoc sessions)
        _hub.On<Guid, string>(SignalingEvents.ReceiveAnswer, (sessionId, sdp) =>
            AnswerReceived?.Invoke(this, new SdpMessage { Type = "answer", SessionId = sessionId, Sdp = sdp }));

        _hub.On<Guid, string, string, int?>(SignalingEvents.ReceiveIceCandidate,
            (sessionId, candidate, sdpMid, sdpMLineIndex) =>
                IceCandidateReceived?.Invoke(this, new IceCandidateMessage
                {
                    SessionId = sessionId,
                    Candidate = candidate,
                    SdpMid = sdpMid,
                    SdpMLineIndex = sdpMLineIndex
                }));

        _hub.On<SessionStateMessage>(SignalingEvents.SessionStateChanged, msg =>
            SessionStateChanged?.Invoke(this, msg));

        _hub.On<Guid>(SignalingEvents.SessionApproved, sessionId =>
            SessionApproved?.Invoke(this, sessionId));

        _hub.On<Guid>(SignalingEvents.SessionRejected, sessionId =>
            SessionRejected?.Invoke(this, sessionId));

        await _hub.StartAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_hub is not null)
            await _hub.StopAsync(ct);
    }

    public async Task RequestSessionAsync(Guid sessionId, Guid machineId, CancellationToken ct = default)
    {
        EnsureConnected();
        await _hub!.InvokeAsync(SignalingEvents.RequestSession, sessionId, machineId, cancellationToken: ct);
    }

    public async Task SendOfferAsync(string targetConnectionId, Guid sessionId, string sdp, CancellationToken ct = default)
    {
        EnsureConnected();
        await _hub!.InvokeAsync(SignalingEvents.SendOffer, sessionId, sdp, targetConnectionId, cancellationToken: ct);
    }

    public async Task SendAnswerAsync(string targetConnectionId, Guid sessionId, string sdp, CancellationToken ct = default)
    {
        EnsureConnected();
        await _hub!.InvokeAsync(SignalingEvents.SendAnswer, sessionId, sdp, targetConnectionId, cancellationToken: ct);
    }

    public async Task SendIceCandidateAsync(string targetConnectionId, Guid sessionId, string candidate, string sdpMid, int? sdpMLineIndex, CancellationToken ct = default)
    {
        EnsureConnected();
        await _hub!.InvokeAsync(SignalingEvents.SendIceCandidate, sessionId, candidate, sdpMid, sdpMLineIndex, targetConnectionId, cancellationToken: ct);
    }

    public async Task EndSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        EnsureConnected();
        await _hub!.InvokeAsync(SignalingEvents.EndSession, sessionId, cancellationToken: ct);
    }

    private void EnsureConnected()
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected)
            throw new InvalidOperationException("SignalR hub is not connected.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
