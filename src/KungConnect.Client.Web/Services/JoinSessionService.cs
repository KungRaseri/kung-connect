using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using KungConnect.Shared.Constants;
using KungConnect.Shared.Signaling;
using System.Text.Json;

namespace KungConnect.Client.Web.Services;

/// <summary>
/// Orchestrates the browser-based join-code session for the customer.
///
/// Flow (operator-as-offerer model):
///   1. Customer's Join.razor calls ConnectAsync(serverUrl, code) — connects hub + registers code.
///   2. Operator's desktop connects, sends SDP offer (contains "video" + "input" data channels).
///   3. HandleOfferAsync: requests getDisplayMedia, initialises RTCPeerConnection, creates SDP answer.
///   4. "video" data channel opens → browser starts sending JPEG frames to operator.
///   5. "input" data channel → operator can send input events (no-op for security).
/// </summary>
public sealed class JoinSessionService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private HubConnection? _hub;
    private DotNetObjectReference<JoinSessionService>? _dotnetRef;

    // Filled in when the offer arrives so ICE candidates are routed correctly
    private Guid _sessionId;
    private string? _operatorConnectionId;

    // Expose to the UI
    public string? JoinCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ConnectionState { get; private set; } = "idle";

    // Notify Blazor component to re-render
    public event Action? StateChanged;

    public JoinSessionService(IJSRuntime js) => _js = js;

    /// <summary>
    /// Connect to the hub and register the customer's join code.
    /// <paramref name="code"/> is the 6-digit code provided by the operator
    /// (customer entered it in Join.razor).
    /// </summary>
    public async Task ConnectAsync(string serverUrl, string code, CancellationToken ct = default)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}/hubs/signaling")
            .WithAutomaticReconnect()
            .Build();

        // Operator sends us an SDP offer once they have connected
        _hub.On<Guid, string, string>(SignalingEvents.ReceiveOffer,
            async (sessionId, sdp, senderConnId) =>
            {
                _sessionId = sessionId;
                _operatorConnectionId = senderConnId;
                await HandleOfferAsync(sdp, senderConnId);
            });

        // Forward ICE candidates from operator into the browser peer
        _hub.On<Guid, string, string, int?>(SignalingEvents.ReceiveIceCandidate,
            async (_, candidate, sdpMid, sdpMLineIndex) =>
            {
                await _js.InvokeVoidAsync("KungConnectJoin.addIceCandidate",
                    JsonSerializer.Serialize(new { candidate, sdpMid, sdpMLineIndex }));
            });

        await _hub.StartAsync(ct);

        // Register with the hub using the operator-supplied code
        await _hub.InvokeAsync(SignalingEvents.JoinCodeCreate, code, cancellationToken: ct);

        JoinCode = code;
        ConnectionState = "waiting";
        Notify();
    }

    private async Task HandleOfferAsync(string offerSdp, string senderConnId)
    {
        try
        {
            _dotnetRef ??= DotNetObjectReference.Create(this);
            ConnectionState = "capturing";
            Notify();

            // Request screen capture permission (shows the browser's share-screen dialog)
            await _js.InvokeVoidAsync("KungConnectJoin.startCapture", _dotnetRef);

            // Initialise RTCPeerConnection — data channels ("video", "input") come via ondatachannel
            await _js.InvokeVoidAsync("KungConnectJoin.initPeer",
                JsonSerializer.Serialize(new object[]
                {
                    new { urls = "stun:stun.l.google.com:19302" }
                }));

            // Set operator's offer as remote description, then create our answer
            await _js.InvokeVoidAsync("KungConnectJoin.setOffer", offerSdp);
            var answerSdp = await _js.InvokeAsync<string>("KungConnectJoin.createAnswer");

            // Send SDP answer back via hub, targeting the operator's connection directly
            await _hub!.InvokeAsync(
                SignalingEvents.SendAnswer,
                _sessionId,
                answerSdp,
                senderConnId);

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

    // ── JSInvokable callbacks ────────────────────────────────────────────────

    /// <summary>Called from JS when the user clicks "Stop sharing".</summary>
    [JSInvokable]
    public void OnCaptureStopped()
    {
        ConnectionState = "stopped";
        Notify();
    }

    /// <summary>Called from JS with trickled ICE candidates gathered by this browser.</summary>
    [JSInvokable]
    public async void OnIceCandidate(string candidateJson)
    {
        if (_hub is null || _operatorConnectionId is null) return;
        try
        {
            var c = JsonSerializer.Deserialize<JsonElement>(candidateJson);
            await _hub.InvokeAsync(
                SignalingEvents.SendIceCandidate,
                _sessionId,
                c.GetProperty("candidate").GetString() ?? string.Empty,
                c.TryGetProperty("sdpMid", out var mid) ? mid.GetString() ?? "" : "",
                c.TryGetProperty("sdpMLineIndex", out var idx) ? (int?)idx.GetInt32() : null,
                _operatorConnectionId);
        }
        catch { /* best-effort ICE trickle */ }
    }

    /// <summary>Called from JS when the WebRTC connection state changes.</summary>
    [JSInvokable]
    public void OnConnectionStateChange(string state)
    {
        ConnectionState = state switch
        {
            "connected"     => "active",
            "disconnected"  => "stopped",
            "failed"        => "error",
            _               => state
        };
        Notify();
    }

    /// <summary>
    /// Called from JS when an input event arrives on the "input" data channel.
    /// Remote control of the customer's machine requires explicit consent — intentionally no-op.
    /// </summary>
    [JSInvokable]
    public void OnInputEvent(string eventJson) { /* consent gate — not implemented */ }

    private void Notify() => StateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        try { await _js.InvokeVoidAsync("KungConnectJoin.cleanup"); } catch { }
        _dotnetRef?.Dispose();
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
