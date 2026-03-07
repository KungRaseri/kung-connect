namespace KungConnect.Shared.Constants;

/// <summary>
/// SignalR hub method and event names shared across Server, Agent, and Client.
/// Centralising these prevents typo-driven bugs.
/// </summary>
public static class SignalingEvents
{
    // ── Client → Server (invoke) ─────────────────────────────────────────────
    public const string SendOffer        = "SendOffer";
    public const string SendAnswer       = "SendAnswer";
    public const string SendIceCandidate = "SendIceCandidate";
    public const string AgentRegister    = "AgentRegister";
    public const string AgentHeartbeat   = "AgentHeartbeat";
    public const string RequestSession   = "RequestSession";
    public const string ApproveSession   = "ApproveSession";
    public const string RejectSession    = "RejectSession";
    public const string EndSession       = "EndSession";
    public const string JoinCodeCreate   = "JoinCodeCreate";
    public const string JoinCodeConnect  = "JoinCodeConnect";

    // ── Server → Client/Agent (receive) ─────────────────────────────────────
    public const string ReceiveOffer         = "ReceiveOffer";
    public const string ReceiveAnswer        = "ReceiveAnswer";
    public const string ReceiveIceCandidate  = "ReceiveIceCandidate";
    public const string SessionRequested     = "SessionRequested";
    public const string SessionApproved      = "SessionApproved";
    public const string SessionRejected      = "SessionRejected";
    public const string SessionEnded         = "SessionEnded";
    public const string SessionStateChanged  = "SessionStateChanged";
    public const string MachineStatusChanged = "MachineStatusChanged";
    public const string Error                = "Error";

    // ── Update notifications ─────────────────────────────────────────────────
    /// <summary>Agent → Server (invoke): agent has detected a newer GitHub release.
    /// Args: machineSecret, latestVersion, downloadUrl.</summary>
    public const string AgentUpdateAvailable   = "AgentUpdateAvailable";
    /// <summary>Server → Dashboard clients (receive): a machine has a pending update.
    /// Args: machineId (Guid), latestVersion (string), downloadUrl (string).</summary>
    public const string MachineUpdateAvailable = "MachineUpdateAvailable";
    /// <summary>Server → Agent: dashboard user requested an immediate update check. No args.</summary>
    public const string CheckForUpdates        = "CheckForUpdates";
    /// <summary>Agent → Server: result of an update check.
    /// Args: machineSecret (string), status ("up-to-date" | "github-not-configured").</summary>
    public const string AgentUpdateCheckStatus = "AgentUpdateCheckStatus";

    // ── Join-code flow (both directions) ────────────────────────────────────
    /// <summary>Server → Admin: customer has connected with the code. Payload: targetConnectionId.</summary>
    public const string JoinCodeCustomerReady = "JoinCodeCustomerReady";
    /// <summary>Server → Customer: admin has started the session. Payload: sessionId.</summary>
    public const string JoinCodeOperatorReady = "JoinCodeOperatorReady";
    /// <summary>Server → Admin: sent back when the code was successfully registered. Payload: code, expiresAt.</summary>
    public const string JoinCodeCreated       = "JoinCodeCreated";
    /// <summary>Server → Admin/Customer: the other side already joined — used for late-join handshake.</summary>
    public const string JoinCodeAccepted      = "JoinCodeAccepted";
    /// <summary>Server → Customer: operator has connected via the code.</summary>
    public const string OperatorJoined        = "OperatorJoined";
}
