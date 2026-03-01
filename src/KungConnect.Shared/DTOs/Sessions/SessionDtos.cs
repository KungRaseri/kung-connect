using KungConnect.Shared.Enums;
using KungConnect.Shared.Models;

namespace KungConnect.Shared.DTOs.Sessions;

public record RequestSessionDto(Guid MachineId, bool ViewOnly = false);

public record SessionDto(
    Guid Id,
    Guid MachineId,
    string MachineName,
    SessionState State,
    ConnectionType ConnectionType,
    bool IsViewOnly,
    DateTimeOffset StartedAt)
{
    public static SessionDto FromInfo(SessionInfo s) =>
        new(s.Id, s.MachineId, s.MachineName, s.State, s.ConnectionType, s.IsViewOnly, s.StartedAt);
}
