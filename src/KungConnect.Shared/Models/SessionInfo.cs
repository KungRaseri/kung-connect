using KungConnect.Shared.Enums;

namespace KungConnect.Shared.Models;

public class SessionInfo
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public Guid RequestedByUserId { get; set; }
    public string RequestedByUsername { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; }
    public SessionState State { get; set; }
    public bool IsViewOnly { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}
