using KungConnect.Agent.Configuration;
using KungConnect.Agent.Services;
using KungConnect.Shared.Constants;
using KungConnect.Shared.Signaling;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace KungConnect.Agent;

/// <summary>
/// Main background service: connects to the server, registers the agent,
/// listens for incoming session requests, and manages their lifecycle.
/// </summary>
public class Worker(
    ISignalingClientService signalingClient,
    SessionHandlerService sessionHandler,
    IOptions<AgentOptions> agentOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly AgentOptions _opts = agentOptions.Value;
    private readonly Dictionary<Guid, CancellationTokenSource> _sessions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("KungConnect Agent starting. Server: {Url}", _opts.ServerUrl);

        await signalingClient.StartAsync(stoppingToken);
        RegisterHubHandlers(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (signalingClient.IsConnected)
                    await signalingClient.Connection.InvokeAsync(
                        SignalingEvents.AgentHeartbeat, _opts.MachineSecret, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Heartbeat failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        await signalingClient.StopAsync(stoppingToken);
    }

    private void RegisterHubHandlers(CancellationToken stoppingToken)
    {
        var conn = signalingClient.Connection;

        conn.On<Guid, string>(SignalingEvents.SessionRequested, (sessionId, operatorConnId) =>
        {
            logger.LogInformation("Session {Id} requested by operator {ConnId}", sessionId, operatorConnId);
            if (_opts.AutoAcceptSessions)
                AcceptSession(sessionId, operatorConnId, stoppingToken);
            else
                logger.LogWarning("Session {Id}: AutoAcceptSessions=false – implement UI approval.", sessionId);
        });

        conn.On<Guid, string>(SignalingEvents.ReceiveAnswer, (sessionId, _) =>
            logger.LogDebug("ReceiveAnswer for {Id} — routed by SessionHandlerService", sessionId));

        conn.On<Guid, string, string, int?>(SignalingEvents.ReceiveIceCandidate,
            (sessionId, _, _, _) =>
            logger.LogDebug("ReceiveIceCandidate for {Id} — routed by SessionHandlerService", sessionId));

        conn.On<Guid>(SignalingEvents.SessionEnded, sessionId =>
        {
            if (_sessions.TryGetValue(sessionId, out var cts))
            {
                cts.Cancel();
                _sessions.Remove(sessionId);
                logger.LogInformation("Session {Id} terminated", sessionId);
            }
        });
    }

    private void AcceptSession(Guid sessionId, string operatorConnId, CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _sessions[sessionId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await signalingClient.Connection.InvokeAsync(
                    SignalingEvents.ApproveSession, sessionId, operatorConnId, cts.Token);
                await sessionHandler.HandleSessionAsync(sessionId, operatorConnId, signalingClient, cts.Token);
            }
            catch (Exception ex) { logger.LogError(ex, "Session {Id} failed", sessionId); }
            finally { _sessions.Remove(sessionId); cts.Dispose(); }
        }, cts.Token);
    }
}

