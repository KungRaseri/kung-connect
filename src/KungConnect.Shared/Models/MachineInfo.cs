using KungConnect.Shared.Enums;

namespace KungConnect.Shared.Models;

public class MachineInfo
{
    public Guid Id { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public OsType OsType { get; set; }
    public MachineStatus Status { get; set; }
    public string AgentVersion { get; set; } = string.Empty;
    public DateTimeOffset LastSeen { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
}
