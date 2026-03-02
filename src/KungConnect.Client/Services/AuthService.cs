using System.Net.Http.Json;
using KungConnect.Shared.DTOs.Auth;

namespace KungConnect.Client.Services;

public sealed class AuthService : IAuthService, IDisposable
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private string? _refreshToken;

    public string? AccessToken => _accessToken;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    public AuthService(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("KungConnect");
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty login response.");

        _accessToken = result.AccessToken;
        _refreshToken = result.RefreshToken;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        return result;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (_refreshToken is null) throw new InvalidOperationException("No refresh token.");

        var response = await _http.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(_refreshToken), ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty refresh response.");

        _accessToken = result.AccessToken;
        _refreshToken = result.RefreshToken;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (_refreshToken is not null)
        {
            try
            {
                await _http.PostAsJsonAsync("/api/auth/logout",
                    new RefreshTokenRequest(_refreshToken), ct);
            }
            catch { /* best-effort */ }
        }

        _accessToken = null;
        _refreshToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public void SetTokens(string accessToken, string? refreshToken = null)
    {
        _accessToken  = accessToken;
        _refreshToken = refreshToken;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    public void Dispose() => _http.Dispose();
}
