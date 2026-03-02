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
/// - Creates RTCPeerConnection (SIPSorcery)
/// - "video" data channel: sends JPEG-encoded screen frames (10 fps)
/// - "input" data channel: receives InputEvent JSON from operator
/// - Registers ReceiveAnswer / ReceiveIceCandidate hub handlers scoped to this session
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

        // ── Data channel: "video" (agent sends JPEG frames) ──────────────────
        var videoDc = await pc.createDataChannel("video",
            new RTCDataChannelInit { ordered = false, maxRetransmits = 0 });

        // Subscribe to FrameCaptured — send JPEG bytes on the data channel
        void OnFrameCaptured(object? s, FrameCapturedEventArgs args)
        {
            if (videoDc.readyState == RTCDataChannelState.open)
                videoDc.send(args.Data);
        }

        pc.onconnectionstatechange += state =>
        {
            logger.LogInformation("Session {Id} WebRTC state: {State}", sessionId, state);
            if (state == RTCPeerConnectionState.connected)
            {
                capturer.FrameCaptured += OnFrameCaptured;
                _ = capturer.StartAsync(cancellationToken: ct);
            }
            else if (state is RTCPeerConnectionState.disconnected
                            or RTCPeerConnectionState.failed
                            or RTCPeerConnectionState.closed)
            {
                capturer.FrameCaptured -= OnFrameCaptured;
            }
        };

        // ── Data channel: "input" (operator sends events to agent) ──────────
        var inputDc = await pc.createDataChannel("input",
            new RTCDataChannelInit { ordered = true });

        inputDc.onmessage += (_, _, data) =>
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

        // ── ICE candidate trickle (agent → operator) ─────────────────────────
        pc.onicecandidate += candidate =>
        {
            if (candidate is null) return;
            _ = signalingClient.Connection.InvokeAsync(
                SignalingEvents.SendIceCandidate,
                sessionId,
                candidate.candidate,
                candidate.sdpMid ?? string.Empty,
                candidate.sdpMLineIndex,
                operatorConnectionId,
                ct);
        };

        pc.ondatachannel += receivedDc =>
            logger.LogDebug("Unexpected incoming data channel: {Label}", receivedDc.label);

        // ── Register hub handlers scoped to this session ─────────────────────
        // These fire when the operator sends back the answer and ICE candidates.
        var conn = signalingClient.Connection;

        conn.On<Guid, string>(SignalingEvents.ReceiveAnswer, (sid, sdp) =>
        {
            if (sid != sessionId) return;
            logger.LogDebug("Session {Id}: received SDP answer", sessionId);
            var result = pc.setRemoteDescription(
                new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
            if (result != SetDescriptionResultEnum.OK)
                logger.LogWarning("Session {Id}: setRemoteDescription(answer) returned {Result}", sessionId, result);
        });

        conn.On<Guid, string, string, int?>(SignalingEvents.ReceiveIceCandidate,
            (sid, candidate, sdpMid, sdpMLineIndex) =>
            {
                if (sid != sessionId) return;
                try
                {
                    pc.addIceCandidate(new RTCIceCandidateInit
                    {
                        candidate     = candidate,
                        sdpMid        = sdpMid,
                        sdpMLineIndex = (ushort)(sdpMLineIndex ?? 0)
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Session {Id}: addIceCandidate failed", sessionId);
                }
            });

        // ── Build SDP offer and send to operator ──────────────────────────────
        var offer = pc.createOffer();
        await pc.setLocalDescription(offer);

        logger.LogDebug("Sending SDP offer for session {Id}", sessionId);
        await signalingClient.Connection.InvokeAsync(
            SignalingEvents.SendOffer,
            sessionId,
            offer.sdp,
            operatorConnectionId,
            ct);

        // ── Wait for session end (cancellation from Worker) ───────────────────
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }

        capturer.FrameCaptured -= OnFrameCaptured;
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

