using KungConnect.Agent.Configuration;
using KungConnect.Agent.Services;
using KungConnect.Shared.Constants;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        if (string.IsNullOrWhiteSpace(_opts.ServerUrl))
        {
            logger.LogCritical("Agent.ServerUrl is not configured. Set it in appsettings.json.");
            return;
        }

        // First run: generate and persist a stable machine identity.
        // The server will create a machine record the first time this secret is seen.
        if (string.IsNullOrWhiteSpace(_opts.MachineSecret))
        {
            var secret = Guid.NewGuid().ToString("N"); // 32-char hex
            logger.LogInformation("First run — generating machine identity and persisting to appsettings.json.");
            PersistMachineSecret(secret);
            _opts.MachineSecret = secret;
        }

        logger.LogInformation("KungConnect Agent starting. Server: {Url}", _opts.ServerUrl);

        await signalingClient.StartAsync(stoppingToken);
        RegisterHubHandlers(stoppingToken);
        await signalingClient.RegisterAsync(stoppingToken);

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

    private void PersistMachineSecret(string secret)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("appsettings.json not found at {Path} — MachineSecret not persisted. " +
                              "Set Agent__MachineSecret={Secret} via environment variable.", path, secret);
            return;
        }
        try
        {
            var json     = File.ReadAllText(path);
            var root     = JsonNode.Parse(json) as JsonObject
                           ?? throw new InvalidOperationException("Root is not a JSON object.");
            root.TryGetPropertyValue("Agent", out var agentNode);
            var agentObj = (agentNode as JsonObject) ?? new JsonObject();
            agentObj["MachineSecret"] = secret;
            root["Agent"] = agentObj;
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            logger.LogInformation("Machine identity saved to {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write MachineSecret to appsettings.json. " +
                               "Set Agent__MachineSecret={Secret} manually.", secret);
        }
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

        // NOTE: ReceiveAnswer and ReceiveIceCandidate are registered per-session
        // inside SessionHandlerService.HandleSessionAsync with proper session-ID guards.
        // Worker does NOT register them here to avoid duplicate dispatch.

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

