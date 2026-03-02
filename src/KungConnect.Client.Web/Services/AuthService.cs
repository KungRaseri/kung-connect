using System.Net.Http.Json;
using System.Text.Json;
using KungConnect.Shared.DTOs.Auth;
using Microsoft.JSInterop;

namespace KungConnect.Client.Web.Services;

/// <summary>
/// Manages JWT storage (localStorage) and exposes the current identity to the app.
/// All dashboard pages inject this and call EnsureAuthenticatedAsync on init.
/// </summary>
public sealed class AuthService(HttpClient http, IJSRuntime js)
{
    private const string AccessTokenKey  = "kc_access_token";
    private const string RefreshTokenKey = "kc_refresh_token";
    private const string UsernameKey     = "kc_username";
    private const string RolesKey        = "kc_roles";

    private string? _accessToken;
    private string? _username;
    private string[]? _roles;
    private bool _initialized;

    public string? AccessToken => _accessToken;
    public string? Username    => _username;
    public string[]? Roles     => _roles;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
    public bool IsAdmin => _roles?.Contains("admin") ?? false;

    /// <summary>Loads tokens from localStorage into memory. Safe to call multiple times.
    /// If the stored access token is expired it is cleared immediately so that
    /// <see cref="IsAuthenticated"/> returns false and the layout guard redirects to login.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _accessToken = await js.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);

        if (_accessToken is not null && IsTokenExpired(_accessToken))
        {
            // Clear everything — IsAuthenticated will return false and the
            // DashboardLayout will redirect to /login.
            await LogoutAsync();
            return;
        }

        _username    = await js.InvokeAsync<string?>("localStorage.getItem", UsernameKey);
        var rolesJson = await js.InvokeAsync<string?>("localStorage.getItem", RolesKey);
        _roles = rolesJson is not null
            ? JsonSerializer.Deserialize<string[]>(rolesJson)
            : null;
        _initialized = true;
    }

    /// <summary>
    /// Decodes the JWT payload (no signature check — we only need the <c>exp</c> claim
    /// for a client-side freshness guard; the server always re-validates the signature).
    /// </summary>
    private static bool IsTokenExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;

            // Base64url → Base64
            var payload = parts[1]
                .Replace('-', '+').Replace('_', '/');
            payload += (payload.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("exp", out var exp)) return false;

            return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) <= DateTimeOffset.UtcNow;
        }
        catch { return true; }
    }

    /// <summary>
    /// Posts credentials to /api/auth/login. On success stores tokens and returns true.
    /// </summary>
    public async Task<(bool Success, string? Error)> LoginAsync(string username, string password)
    {
        try
        {
            var resp = await http.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(username, password));

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<JsonElement>();
                return (false, err.TryGetProperty("message", out var m) ? m.GetString() : "Login failed");
            }

            var login = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (login is null) return (false, "Empty response");

            await StoreTokensAsync(login);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Clears all stored tokens and resets in-memory state.</summary>
    public async Task LogoutAsync()
    {
        _accessToken = null;
        _username    = null;
        _roles       = null;
        _initialized = false;
        await js.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", UsernameKey);
        await js.InvokeVoidAsync("localStorage.removeItem", RolesKey);
    }

    /// <summary>Attaches the Bearer token to every HttpClient request.</summary>
    public void ApplyAuthHeader()
    {
        http.DefaultRequestHeaders.Authorization = _accessToken is not null
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken)
            : null;
    }

    private async Task StoreTokensAsync(LoginResponse login)
    {
        _accessToken = login.AccessToken;
        _username    = login.Username;
        _roles       = login.Roles;
        _initialized = true;

        await js.InvokeVoidAsync("localStorage.setItem", AccessTokenKey,  login.AccessToken);
        await js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, login.RefreshToken);
        await js.InvokeVoidAsync("localStorage.setItem", UsernameKey,     login.Username);
        await js.InvokeVoidAsync("localStorage.setItem", RolesKey,        JsonSerializer.Serialize(login.Roles));

        ApplyAuthHeader();
    }
}
