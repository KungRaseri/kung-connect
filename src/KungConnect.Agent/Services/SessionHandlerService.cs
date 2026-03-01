using KungConnect.Agent.Capture;
using KungConnect.Agent.Configuration;
using KungConnect.Agent.Input;
using KungConnect.Agent.Services;
using KungConnect.Shared.Constants;
using KungConnect.Shared.Interfaces;
using KungConnect.Shared.Signaling;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;
using System.Text.Json;

namespace KungConnect.Agent.Services;

/// <summary>
/// Handles the full lifecycle of an individual remote-control session:
/// - WebRTC peer connection setup (SIPSorcery)
/// - Video track from IScreenCapturer
/// - Input data channel → IInputInjector
/// </summary>
public class SessionHandlerService(
    IOptions<AgentOptions> agentOptions,
    ILoggerFactory loggerFactory,
    ILogger<SessionHandlerService> logger)
{
    private readonly AgentOptions _opts = agentOptions.Value;

    public async Task HandleSessionAsync(
        Guid sessionId,
        string operatorConnectionId,
        ISignalingClientService signalingClient,
        CancellationToken ct)
    {
        logger.LogInformation("Starting session {Id} for operator {ConnId}", sessionId, operatorConnectionId);

        var capturer = ScreenCapturerFactory.Create(loggerFactory);
        var injector = InputInjectorFactory.Create(loggerFactory);

        var pc = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = _opts.ServerUrl.Contains("localhost")
                ? []
                : [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }]
        });

        // ── Data channel for input events ────────────────────────────────────
        var dc = await pc.createDataChannel("input", new RTCDataChannelInit { ordered = true });
        dc.onmessage += (_, _, data) =>
        {
            try
            {
                var evt = JsonSerializer.Deserialize<InputEvent>(data);
                if (evt is null) return;
                ProcessInputEvent(evt, injector);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process input event");
            }
        };

        // ── ICE candidate trickle ─────────────────────────────────────────────
        pc.onicecandidate += candidate =>
        {
            if (candidate is null) return;
            _ = signalingClient.Connection.InvokeAsync(
                SignalingEvents.SendIceCandidate,
                sessionId, candidate.candidate,
                candidate.sdpMid ?? string.Empty,
                candidate.sdpMLineIndex,
                operatorConnectionId, ct);
        };

        // ── Connection state ──────────────────────────────────────────────────
        pc.onconnectionstatechange += state =>
        {
            logger.LogInformation("Session {Id} WebRTC state: {State}", sessionId, state);
            if (state == RTCPeerConnectionState.connected)
                _ = capturer.StartAsync(cancellationToken: ct);
        };

        pc.ondatachannel += receivedDc => logger.LogDebug("Data channel received: {Label}", receivedDc.label);

        // ── Build SDP offer ───────────────────────────────────────────────────
        var offer = pc.createOffer();
        await pc.setLocalDescription(offer);

        logger.LogDebug("Sending SDP offer for session {Id}", sessionId);
        await signalingClient.Connection.InvokeAsync(
            SignalingEvents.SendOffer,
            sessionId, offer.sdp, operatorConnectionId, ct);

        // ── Wait for answer via hub handler (registered in Worker) ───────────
        // The hub handler will call pc.setRemoteDescription(answer) externally.
        // Session runs until cancellation.
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }

        pc.close();
        await capturer.StopAsync();
        capturer.Dispose();

        logger.LogInformation("Session {Id} ended", sessionId);
    }

    private static void ProcessInputEvent(InputEvent evt, IInputInjector injector)
    {
        switch (evt.EventType)
        {
            case "mouse-move":
                injector.MoveMouse(evt.X ?? 0, evt.Y ?? 0);
                break;
            case "mouse-down":
                injector.MouseDown((Shared.Enums.MouseButton)(evt.Button ?? 0));
                break;
            case "mouse-up":
                injector.MouseUp((Shared.Enums.MouseButton)(evt.Button ?? 0));
                break;
            case "scroll":
                injector.Scroll(evt.DeltaX ?? 0, evt.DeltaY ?? 0);
                break;
            case "key-down":
                injector.KeyDown(evt.KeyCode ?? 0);
                break;
            case "key-up":
                injector.KeyUp(evt.KeyCode ?? 0);
                break;
            case "clipboard":
                if (evt.Text is not null)
                    injector.TypeText(evt.Text);
                break;
        }
    }
}
