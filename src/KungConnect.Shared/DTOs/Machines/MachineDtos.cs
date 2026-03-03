using KungConnect.Shared.Enums;
using KungConnect.Shared.Models;

namespace KungConnect.Shared.DTOs.Machines;

/// <summary>Operator creates a machine record in the dashboard; agent uses the returned secret to connect.</summary>
public record ProvisionMachineRequest(string Alias);

/// <summary>Returned after provisioning. <see cref="ConfigSnippet"/> is ready to paste into the agent's appsettings.json.</summary>
public record ProvisionMachineResponse(Guid MachineId, string Secret, string ConfigSnippet);

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
