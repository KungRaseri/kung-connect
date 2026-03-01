using System.Net.Http.Json;
using KungConnect.Shared.DTOs.Machines;

namespace KungConnect.Client.Services;

public sealed class MachineService(IHttpClientFactory factory) : IMachineService
{
    private HttpClient Http => factory.CreateClient("KungConnect");

    public async Task<IReadOnlyList<MachineDto>> GetMachinesAsync(CancellationToken ct = default)
    {
        var result = await Http.GetFromJsonAsync<List<MachineDto>>("/api/machines", ct);
        return result ?? [];
    }

    public async Task<MachineDto?> GetMachineAsync(Guid id, CancellationToken ct = default)
        => await Http.GetFromJsonAsync<MachineDto>($"/api/machines/{id}", ct);

    public async Task DeleteMachineAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.DeleteAsync($"/api/machines/{id}", ct);
        response.EnsureSuccessStatusCode();
    }
}
