using BCrypt.Net;
using KungConnect.Server.Configuration;
using KungConnect.Server.Data;
using KungConnect.Server.Data.Entities;
using KungConnect.Server.Services;
using KungConnect.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KungConnect.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext db,
    IJwtService jwtService,
    IOptions<JwtOptions> jwtOptions,
    IOptions<ServerOptions> serverOptions,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password" });

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!serverOptions.Value.AllowSelfRegistration)
            return Forbid();

        if (await db.Users.AnyAsync(u => u.Username == request.Username))
            return Conflict(new { message = "Username already taken" });

        var user = new UserEntity
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Roles = ["operator"]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        logger.LogInformation("New user registered: {Username}", user.Username);

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u =>
                u.RefreshToken == request.RefreshToken &&
                u.RefreshTokenExpiresAt > DateTimeOffset.UtcNow);

        if (user is null)
            return Unauthorized(new { message = "Invalid or expired refresh token" });

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
        var user = await db.Users.FindAsync(userId);
        if (user is not null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<LoginResponse> IssueTokensAsync(UserEntity user)
    {
        var accessToken  = jwtService.GenerateAccessToken(user);
        var refreshToken = jwtService.GenerateRefreshToken();
        var expiresAt    = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.Value.ExpiryMinutes);

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpiryDays);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return new LoginResponse(accessToken, refreshToken, expiresAt, user.Username, user.Roles);
    }
}
