namespace KungConnect.Shared.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string Username,
    string[] Roles);

public record RefreshTokenRequest(string RefreshToken);

public record RegisterRequest(string Username, string Email, string Password);
