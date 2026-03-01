using KungConnect.Shared.DTOs.Machines;

namespace KungConnect.Client.Services;

public interface IMachineService
{
    Task<IReadOnlyList<MachineDto>> GetMachinesAsync(CancellationToken ct = default);
    Task<MachineDto?> GetMachineAsync(Guid id, CancellationToken ct = default);
    Task DeleteMachineAsync(Guid id, CancellationToken ct = default);
}
