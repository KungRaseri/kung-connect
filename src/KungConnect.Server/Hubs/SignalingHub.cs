using System.Security.Claims;
using KungConnect.Server.Data;
using KungConnect.Server.Services;
using KungConnect.Shared.Constants;
using KungConnect.Shared.Enums;
using KungConnect.Shared.Signaling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KungConnect.Server.Hubs;

/// <summary>
/// Central SignalR hub that routes WebRTC signaling between Clients and Agents,
/// and also handles agentless join-code sessions (browser targets).
/// </summary>
[Authorize]
public class SignalingHub(
    AppDbContext db,
    IMachineRegistry machineRegistry,
    IJoinCodeService joinCodeService,
    ILogger<SignalingHub> logger) : Hub
{
    // ── Connection lifecycle ─────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        logger.LogDebug("SignalR connected: {ConnId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await machineRegistry.SetOfflineAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
        logger.LogDebug("SignalR disconnected: {ConnId}", Context.ConnectionId);
    }

    // ── Agent registration ───────────────────────────────────────────────────

    [AllowAnonymous]
    public async Task AgentRegister(
        string machineSecret,
        string? hostname     = null,
        string? osType       = null,
        string? agentVersion = null)
    {
        var machine = await machineRegistry.AuthenticateAsync(machineSecret);
        if (machine is null)
        {
            await Clients.Caller.SendAsync(SignalingEvents.Error, "Invalid machine secret");
            return;
        }

        // Persist system info supplied by the agent on first connect / reconnect
        var dbMachine = await db.Machines.FindAsync(machine.Id);
        if (dbMachine is not null)
        {
            if (!string.IsNullOrEmpty(hostname))     dbMachine.Hostname     = hostname;
            if (!string.IsNullOrEmpty(agentVersion)) dbMachine.AgentVersion = agentVersion;
            if (!string.IsNullOrEmpty(osType) &&
                Enum.TryParse<Shared.Enums.OsType>(osType, ignoreCase: true, out var parsedOs))
                dbMachine.OsType = parsedOs;
            await db.SaveChangesAsync();
        }

        await machineRegistry.SetOnlineAsync(machine.Id, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"machine:{machine.Id}");

        // Notify all operators watching this machine
        await Clients.Others.SendAsync(SignalingEvents.MachineStatusChanged, machine.Id, "Online");
    }

    [AllowAnonymous]
    public async Task AgentHeartbeat(string machineSecret)
    {
        var machine = await machineRegistry.AuthenticateAsync(machineSecret);
        if (machine is null) return;
        machine.LastSeen = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    // ── Session approval flow ────────────────────────────────────────────────

    /// <summary>Operator requests a session. Server forwards to the agent.</summary>
    public async Task RequestSession(Guid sessionId, Guid machineId)
    {
        var agentConnId = await machineRegistry.GetConnectionIdAsync(machineId);
        if (agentConnId is null)
        {
            await Clients.Caller.SendAsync(SignalingEvents.Error, "Machine is offline");
            return;
        }
        // Track operator for this session
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}:operator");
        await Clients.Client(agentConnId)
            .SendAsync(SignalingEvents.SessionRequested, sessionId, Context.ConnectionId);
    }

    public async Task ApproveSession(Guid sessionId, string operatorConnectionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}:agent");
        var session = await db.Sessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.State = Shared.Enums.SessionState.Approved;
            await db.SaveChangesAsync();
        }
        await Clients.Client(operatorConnectionId)
            .SendAsync(SignalingEvents.SessionApproved, sessionId);
    }

    public async Task RejectSession(Guid sessionId, string operatorConnectionId)
    {
        await Clients.Client(operatorConnectionId)
            .SendAsync(SignalingEvents.SessionRejected, sessionId);
    }

    // ── WebRTC signaling ─────────────────────────────────────────────────────

    public async Task SendOffer(Guid sessionId, string sdp, string targetConnectionId)
    {
        await Clients.Client(targetConnectionId)
            .SendAsync(SignalingEvents.ReceiveOffer, sessionId, sdp, Context.ConnectionId);
    }

    public async Task SendAnswer(Guid sessionId, string sdp, string targetConnectionId)
    {
        await Clients.Client(targetConnectionId)
            .SendAsync(SignalingEvents.ReceiveAnswer, sessionId, sdp);
    }

    public async Task SendIceCandidate(Guid sessionId, string candidate, string sdpMid, int? sdpMLineIndex, string targetConnectionId)
    {
        await Clients.Client(targetConnectionId)
            .SendAsync(SignalingEvents.ReceiveIceCandidate, sessionId, candidate, sdpMid, sdpMLineIndex);
    }

    public async Task EndSession(Guid sessionId)
    {
        var session = await db.Sessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.State = Shared.Enums.SessionState.Terminated;
            session.EndedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        await Clients.Group($"session:{sessionId}:agent")
            .SendAsync(SignalingEvents.SessionEnded, sessionId);
        await Clients.Group($"session:{sessionId}:operator")
            .SendAsync(SignalingEvents.SessionEnded, sessionId);
    }

    // ── Join-code (agentless) flow ───────────────────────────────────────────

    /// <summary>
    /// Called by the *customer* browser to register as ready and waiting.
    /// An optional <paramref name="code"/> lets the customer enter a code the admin already generated.
    /// Omitting it creates a new code (unused for the current flow but kept for flexibility).
    /// </summary>
    [AllowAnonymous]
    public async Task JoinCodeCreate(string? code = null)
    {
        // Check whether an operator is already waiting with this code
        if (code is not null)
        {
            var pendingOp = await joinCodeService.GetPendingOperatorAsync(code);
            if (pendingOp is not null)
            {
                // Complete the handshake: customer arrives after admin
                pendingOp.TargetConnectionId = Context.ConnectionId;
                pendingOp.IsConnected = true;
                // (save happens inside RedeemAsync path; use direct update here)
                var opConnId = pendingOp.OperatorConnectionId!;
                await joinCodeService.ExpireAsync(code);

                // Recreate as a fully-connected record
                var result2 = await joinCodeService.CreateAsync(Context.ConnectionId, code);
                await joinCodeService.RedeemAsync(code, opConnId);

                await Clients.Caller.SendAsync(SignalingEvents.JoinCodeCreated, result2.Code, result2.ExpiresAt);
                await Clients.Caller.SendAsync(SignalingEvents.OperatorJoined, opConnId);
                await Clients.Client(opConnId).SendAsync(SignalingEvents.JoinCodeCustomerReady, Context.ConnectionId);
                return;
            }
        }

        var result = await joinCodeService.CreateAsync(Context.ConnectionId, code);
        await Clients.Caller.SendAsync(SignalingEvents.JoinCodeCreated, result.Code, result.ExpiresAt);
    }

    /// <summary>
    /// Called by the *admin* client to start waiting for a customer to enter their code.
    /// Sends the code back to the admin. Once a customer connects, fires JoinCodeCustomerReady.
    /// </summary>
    public async Task JoinCodeConnect(string code)
    {
        // First try: redeem immediately if customer is already waiting
        var entity = await joinCodeService.RedeemAsync(code, Context.ConnectionId);
        if (entity is not null)
        {
            // Customer already connected — tell admin who to signal
            await Clients.Caller.SendAsync(SignalingEvents.JoinCodeCustomerReady, entity.TargetConnectionId);
            // Tell customer the operator is here
            await Clients.Client(entity.TargetConnectionId)
                .SendAsync(SignalingEvents.OperatorJoined, Context.ConnectionId);
            return;
        }

        // Customer not yet connected — create a pending record on behalf of the admin
        // Store admin connId in OperatorConnectionId so we can match when customer arrives
        var pending = await joinCodeService.CreateForOperatorAsync(code, Context.ConnectionId);
        if (pending is null)
        {
            await Clients.Caller.SendAsync(SignalingEvents.Error, "Invalid or expired join code");
            return;
        }
        // Admin will receive JoinCodeCustomerReady when the customer calls JoinCodeCreate(code)
        await Clients.Caller.SendAsync(SignalingEvents.JoinCodeCreated, pending.Code, pending.ExpiresAt);
    }
}
