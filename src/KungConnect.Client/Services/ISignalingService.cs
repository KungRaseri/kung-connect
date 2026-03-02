using KungConnect.Shared.Signaling;

namespace KungConnect.Client.Services;

/// <summary>
/// Client-side SignalR signaling service used by the operator (Avalonia client).
/// Connects to the server hub, sends/receives WebRTC signaling messages,
/// and exposes events for incoming SDP/ICE from the remote agent.
/// </summary>
public interface ISignalingService : IAsyncDisposable
{
    bool IsConnected { get; }

    // Outbound (operator → server → agent/browser)
    Task ConnectAsync(string accessToken, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task RequestSessionAsync(Guid sessionId, Guid machineId, CancellationToken ct = default);
    Task SendOfferAsync(string targetConnectionId, Guid sessionId, string sdp, CancellationToken ct = default);
    Task SendAnswerAsync(string targetConnectionId, Guid sessionId, string sdp, CancellationToken ct = default);
    Task SendIceCandidateAsync(string targetConnectionId, Guid sessionId, string candidate, string sdpMid, int? sdpMLineIndex, CancellationToken ct = default);
    Task EndSessionAsync(Guid sessionId, CancellationToken ct = default);

    // Inbound events (server → operator)
    event EventHandler<SdpMessage>? OfferReceived;
    event EventHandler<SdpMessage>? AnswerReceived;
    event EventHandler<IceCandidateMessage>? IceCandidateReceived;
    event EventHandler<SessionStateMessage>? SessionStateChanged;
    event EventHandler<Guid>? SessionApproved;
    event EventHandler<Guid>? SessionRejected;
}
