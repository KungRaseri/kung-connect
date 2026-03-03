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
        // Need at least one of: a machine secret OR a registration token
        if (string.IsNullOrWhiteSpace(_opts.MachineSecret) && string.IsNullOrWhiteSpace(_opts.RegistrationToken))
        {
            logger.LogCritical(
                "\n  ─────────────────────────────────────────────────────────────────\n" +
                "  Agent is not configured. Choose one of:\n" +
                "  \n" +
                "  Option A – Self-enrollment (recommended):\n" +
                "    Set  Agent__RegistrationToken  to the token from the server dashboard\n" +
                "    The agent will auto-enroll and save its secret on first run.\n" +
                "  \n" +
                "  Option B – Manual provisioning:\n" +
                "    1. Open the KungConnect web dashboard\n" +
                "    2. Click \"Add Machine\" and enter a name for this machine\n" +
                "    3. Copy the generated config snippet into appsettings.json\n" +
                "    4. Restart the agent\n" +
                "  ─────────────────────────────────────────────────────────────────");
            return;
        }

        logger.LogInformation("KungConnect Agent starting. Server: {Url}", _opts.ServerUrl);

        await signalingClient.StartAsync(stoppingToken);
        RegisterHubHandlers(stoppingToken);

        // ── Self-enrollment: registration token present, no machine secret yet ──
        if (string.IsNullOrWhiteSpace(_opts.MachineSecret) &&
            !string.IsNullOrWhiteSpace(_opts.RegistrationToken))
        {
            logger.LogInformation("No machine secret — starting self-enrollment…");
            try
            {
                var secret = await signalingClient.EnrollAsync(_opts.RegistrationToken, stoppingToken);
                _opts.MachineSecret = secret;         // used immediately for heartbeats etc.
                PersistSecret(secret);                // written to appsettings.json
                logger.LogInformation("Enrollment successful. Machine secret saved.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Self-enrollment failed. Verify that Agent__RegistrationToken matches Server__AgentRegistrationToken.");
                return;
            }
        }

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

    /// <summary>
    /// Writes the received machine secret back into appsettings.json so subsequent
    /// runs authenticate directly without needing to re-enroll.
    /// </summary>
    private void PersistSecret(string secret)
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(settingsPath))
        {
            logger.LogWarning(
                "appsettings.json not found at {Path} — secret not persisted to disk.", settingsPath);
            return;
        }

        try
        {
            var raw      = File.ReadAllText(settingsPath);
            var root     = JsonNode.Parse(raw) as JsonObject
                           ?? throw new InvalidOperationException("Root is not a JSON object.");

            root.TryGetPropertyValue("Agent", out var agentNode);
            var agentObj = (agentNode as JsonObject) ?? new JsonObject();
            agentObj["MachineSecret"]       = secret;
            agentObj["RegistrationToken"]   = string.Empty; // clear token — secret takes over
            root["Agent"] = agentObj;

            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingsPath, root.ToJsonString(opts));
            logger.LogInformation("Machine secret written to {Path}", settingsPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Could not update appsettings.json. Set Agent__MachineSecret={Secret} manually.", secret);
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

