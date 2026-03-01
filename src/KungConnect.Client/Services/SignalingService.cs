using Microsoft.AspNetCore.SignalR.Client;
using KungConnect.Shared.Constants;
using KungConnect.Shared.Signaling;

namespace KungConnect.Client.Services;

public sealed class SignalingService : ISignalingService
{
    private HubConnection? _hub;
    private readonly string _hubUrl;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public event EventHandler<SdpMessage>? AnswerReceived;
    public event EventHandler<IceCandidateMessage>? IceCandidateReceived;
    public event EventHandler<SessionStateMessage>? SessionStateChanged;

    public SignalingService(IHttpClientFactory factory)
    {
        // Resolve the base address from the named client
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

        _hub.On<SdpMessage>(SignalingEvents.ReceiveAnswer, msg =>
            AnswerReceived?.Invoke(this, msg));

        _hub.On<IceCandidateMessage>(SignalingEvents.ReceiveIceCandidate, msg =>
            IceCandidateReceived?.Invoke(this, msg));

        _hub.On<SessionStateMessage>(SignalingEvents.SessionStateChanged, msg =>
            SessionStateChanged?.Invoke(this, msg));

        await _hub.StartAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_hub is not null)
            await _hub.StopAsync(ct);
    }

    public async Task SendOfferAsync(string targetConnectionId, SdpMessage sdp, CancellationToken ct = default)
    {
        EnsureConnected();
        await _hub!.InvokeAsync(SignalingEvents.SendOffer, targetConnectionId, sdp, cancellationToken: ct);
    }

    public async Task SendIceCandidateAsync(string targetConnectionId, IceCandidateMessage ice, CancellationToken ct = default)
    {
        EnsureConnected();
        await _hub!.InvokeAsync(SignalingEvents.SendIceCandidate, targetConnectionId, ice, cancellationToken: ct);
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
