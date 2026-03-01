using KungConnect.Shared.DTOs.Sessions;

namespace KungConnect.Client.Services;

public interface ISessionService
{
    Task<SessionDto> RequestSessionAsync(RequestSessionDto request, CancellationToken ct = default);
    Task<IReadOnlyList<SessionDto>> GetSessionHistoryAsync(CancellationToken ct = default);
    Task<SessionDto?> GetSessionAsync(Guid id, CancellationToken ct = default);
}
