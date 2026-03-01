using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using KungConnect.Shared.Constants;
using KungConnect.Shared.DTOs.Join;
using KungConnect.Shared.Signaling;

namespace KungConnect.Client.Web.Services;

/// <summary>
/// Orchestrates the browser-based join-code session:
/// 1. Connects to the SignalR hub anonymously
/// 2. Creates a join code and shows it to the user
/// 3. When an operator connects, starts getDisplayMedia and WebRTC signaling
/// </summary>
public sealed class JoinSessionService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private HubConnection? _hub;
    private DotNetObjectReference<JoinSessionService>? _dotnetRef;

    // Expose to the UI
    public string? JoinCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ConnectionState { get; private set; } = "idle";

    // Notify Blazor component to re-render
    public event Action? StateChanged;

    public JoinSessionService(IJSRuntime js) => _js = js;

    public async Task ConnectAsync(string serverUrl, CancellationToken ct = default)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}/hubs/signaling")
            .WithAutomaticReconnect()
            .Build();

        // Server sends us SDP offer from operator
        _hub.On<SdpMessage>(SignalingEvents.ReceiveOffer, async msg => await HandleOfferAsync(msg));

        // Server forwards ICE candidates from operator
        _hub.On<IceCandidateMessage>(SignalingEvents.ReceiveIceCandidate, async msg =>
            await _js.InvokeVoidAsync("KungConnectJoin.addIceCandidate",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    candidate = msg.Candidate,
                    sdpMid = msg.SdpMid,
                    sdpMLineIndex = msg.SdpMLineIndex
                }), ct));

        await _hub.StartAsync(ct);

        // Create a join code (anonymous call)
        var result = await _hub.InvokeAsync<CreateJoinCodeResponse>(
            SignalingEvents.JoinCodeCreate, cancellationToken: ct);

        JoinCode = result.Code;
        ConnectionState = "waiting";
        Notify();
    }

    private async Task HandleOfferAsync(SdpMessage msg)
    {
        try
        {
            _dotnetRef = DotNetObjectReference.Create(this);
            ConnectionState = "capturing";
            Notify();

            // Request screen capture permission
            await _js.InvokeVoidAsync("KungConnectJoin.startCapture", _dotnetRef);

            // Init peer with no STUN (local relay through server for MVP)
            await _js.InvokeVoidAsync("KungConnectJoin.initPeer",
                System.Text.Json.JsonSerializer.Serialize(new object[]
                {
                    new { urls = "stun:stun.l.google.com:19302" }
                }));

            // Set remote offer + create answer
            await _js.InvokeVoidAsync("KungConnectJoin.setAnswer", msg.Sdp);

            var answerSdp = await _js.InvokeAsync<string>("KungConnectJoin.createOffer");

            // Send SDP answer back
            await _hub!.InvokeAsync(SignalingEvents.SendAnswer,
                msg.SessionId,
                answerSdp);

            ConnectionState = "connected";
            Notify();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to start session: {ex.Message}";
            ConnectionState = "error";
            Notify();
        }
    }

    // Called from JS when a screen-capture track ends (user clicked Stop Sharing)
    [JSInvokable]
    public void OnCaptureStopped()
    {
        ConnectionState = "stopped";
        Notify();
    }

    // Called from JS with trickled ICE candidates from this browser
    [JSInvokable]
    public async void OnIceCandidate(string candidateJson)
    {
        if (_hub is null) return;
        var c = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(candidateJson);
        await _hub.InvokeAsync(SignalingEvents.SendIceCandidate,
            /* sessionId */ Guid.Empty, // filled in from context when fully wired
            c.GetProperty("candidate").GetString(),
            c.GetProperty("sdpMid").GetString(),
            c.TryGetProperty("sdpMLineIndex", out var idx) ? (int?)idx.GetInt32() : null);
    }

    // Called from JS when WebRTC connection state changes
    [JSInvokable]
    public void OnConnectionStateChange(string state)
    {
        ConnectionState = state;
        Notify();
    }

    // Called from JS when an input event arrives on the data channel
    [JSInvokable]
    public void OnInputEvent(string eventJson)
    {
        // Browser is the agent here — input events come IN from operator
        // and should control the user's machine. For security, this is intentionally
        // left as a no-op stub. Real implementations must show an explicit consent
        // prompt before allowing remote control.
    }

    private void Notify() => StateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        await _js.InvokeVoidAsync("KungConnectJoin.cleanup");
        _dotnetRef?.Dispose();
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
