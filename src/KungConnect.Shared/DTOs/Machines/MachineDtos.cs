using KungConnect.Shared.Enums;
using KungConnect.Shared.Models;

namespace KungConnect.Shared.DTOs.Machines;

public record RegisterMachineRequest(
    string Alias,
    string Hostname,
    OsType OsType,
    string AgentVersion,
    string MachineSecret);

public record MachineHeartbeatRequest(MachineStatus Status);

public record MachineDto(
    Guid Id,
    string Alias,
    string Hostname,
    OsType OsType,
    MachineStatus Status,
    string AgentVersion,
    DateTimeOffset LastSeen)
{
    public static MachineDto FromInfo(MachineInfo m) =>
        new(m.Id, m.Alias, m.Hostname, m.OsType, m.Status, m.AgentVersion, m.LastSeen);
}
