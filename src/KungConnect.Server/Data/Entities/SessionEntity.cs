using KungConnect.Shared.Enums;

namespace KungConnect.Server.Data.Entities;

public class SessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public SessionState State { get; set; } = SessionState.Pending;
    public bool IsViewOnly { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }

    // Navigation
    public MachineEntity Machine { get; set; } = null!;
    public UserEntity? RequestedBy { get; set; }
}
