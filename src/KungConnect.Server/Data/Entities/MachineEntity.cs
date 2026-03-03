using KungConnect.Shared.Enums;

namespace KungConnect.Server.Data.Entities;

public class MachineEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Null for self-registered agents; set when provisioned from the dashboard or claimed by an admin.</summary>
    public Guid? OwnerId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public OsType OsType { get; set; }
    public MachineStatus Status { get; set; } = MachineStatus.Offline;
    public string AgentVersion { get; set; } = string.Empty;
    /// <summary>Secret the agent presents to authenticate itself.</summary>
    public string MachineSecret { get; set; } = string.Empty;
    /// <summary>SignalR connection ID of the currently-connected agent.</summary>
    public string? SignalRConnectionId { get; set; }
    public bool AutoAcceptSessions { get; set; } = false;
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public UserEntity? Owner { get; set; }
    public ICollection<SessionEntity> Sessions { get; set; } = [];
}
