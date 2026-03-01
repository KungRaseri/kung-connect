using KungConnect.Shared.DTOs.Auth;

namespace KungConnect.Client.Services;

public interface IAuthService
{
    string? AccessToken { get; }
    bool IsAuthenticated { get; }

    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
}
