using KungConnect.Shared.Enums;
using KungConnect.Shared.Models;

namespace KungConnect.Shared.DTOs.Machines;

/// <summary>Operator creates a machine record in the dashboard; agent uses the returned secret to connect.</summary>
public record ProvisionMachineRequest(string Alias);

/// <summary>Returned after provisioning. <see cref="ConfigSnippet"/> is ready to paste into the agent's appsettings.json.</summary>
public record ProvisionMachineResponse(Guid MachineId, string Secret, string ConfigSnippet);

/// <summary>Claim an unowned self-registered machine and optionally rename it.</summary>
public record ClaimMachineRequest(string? Alias);

/// <summary>Sent by the agent's --notify-uninstall mode before files are removed. No JWT required; machine secret is the credential.</summary>
public record NotifyUninstallRequest(string MachineSecret);

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
    DateTimeOffset LastSeen,
    /// <summary>Non-null when the agent has reported a newer GitHub release. Null = up-to-date.</summary>
    string? UpdateAvailable = null)
{
    public static MachineDto FromInfo(MachineInfo m) =>
        new(m.Id, m.Alias, m.Hostname, m.OsType, m.Status, m.AgentVersion, m.LastSeen);
}

/// <summary>Full detail view of a machine, including secret and config snippet. Only returned to authenticated owners/admins.</summary>
public record MachineDetailDto(
    Guid Id,
    string Alias,
    string Hostname,
    OsType OsType,
    MachineStatus Status,
    string AgentVersion,
    DateTimeOffset LastSeen,
    DateTimeOffset RegisteredAt,
    string MachineSecret,
    bool AutoAcceptSessions,
    bool IsClaimed,
    string ConfigSnippet,
    string? UpdateAvailable = null);

/// <summary>Payload for PATCH /api/machines/{id} — rename a machine.</summary>
public record UpdateMachineRequest(string Alias);
