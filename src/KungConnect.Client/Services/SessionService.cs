using System.Net.Http.Json;
using KungConnect.Shared.DTOs.Sessions;

namespace KungConnect.Client.Services;

public sealed class SessionService(IHttpClientFactory factory) : ISessionService
{
    private HttpClient Http => factory.CreateClient("KungConnect");

    public async Task<SessionDto> RequestSessionAsync(RequestSessionDto request, CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync("/api/sessions", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty session response.");
    }

    public async Task<IReadOnlyList<SessionDto>> GetSessionHistoryAsync(CancellationToken ct = default)
    {
        var result = await Http.GetFromJsonAsync<List<SessionDto>>("/api/sessions", ct);
        return result ?? [];
    }

    public async Task<SessionDto?> GetSessionAsync(Guid id, CancellationToken ct = default)
        => await Http.GetFromJsonAsync<SessionDto>($"/api/sessions/{id}", ct);
}
