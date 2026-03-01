using KungConnect.Server.Configuration;
using KungConnect.Server.Data;
using KungConnect.Server.Data.Entities;
using KungConnect.Server.Services;
using KungConnect.Shared.Constants;
using KungConnect.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KungConnect.Server.Controllers;

public record SetupRequest(
    string AdminUsername,
    string AdminEmail,
    string AdminPassword,
    string ConfirmPassword,
    string ServerName = "KungConnect");

[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public class SetupController(
    AppDbContext db,
    ISetupService setupService,
    IJwtService jwtService,
    IOptions<JwtOptions> jwtOptions,
    ILogger<SetupController> logger) : ControllerBase
{
    /// <summary>Returns whether the initial setup wizard must be completed.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
        => Ok(new { required = await setupService.IsSetupRequiredAsync() });

    /// <summary>
    /// Completes the initial setup: creates the first admin user and returns
    /// a JWT so the browser can immediately start an authenticated session.
    /// </summary>
    [HttpPost("complete")]
    public async Task<ActionResult<LoginResponse>> Complete([FromBody] SetupRequest request)
    {
        if (!await setupService.IsSetupRequiredAsync())
            return BadRequest(new { error = "Setup has already been completed." });

        // ── Validation ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.AdminUsername) || request.AdminUsername.Length < 3)
            return BadRequest(new { error = "Username must be at least 3 characters." });

        if (string.IsNullOrWhiteSpace(request.AdminEmail) || !request.AdminEmail.Contains('@'))
            return BadRequest(new { error = "A valid email address is required." });

        if (request.AdminPassword != request.ConfirmPassword)
            return BadRequest(new { error = "Passwords do not match." });

        if (request.AdminPassword.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        if (await db.Users.AnyAsync(u => u.Username == request.AdminUsername))
            return Conflict(new { error = "That username is already taken." });

        // ── Create admin user ─────────────────────────────────────────────────
        var user = new UserEntity
        {
            Username     = request.AdminUsername,
            Email        = request.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            Roles        = [Roles.Admin],
        };

        // ── Issue tokens (same pattern as AuthController) ─────────────────────
        var accessToken  = jwtService.GenerateAccessToken(user);
        var refreshToken = jwtService.GenerateRefreshToken();

        user.RefreshToken          = refreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpiryDays);
        user.LastLoginAt           = DateTimeOffset.UtcNow;

        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Cache the result so the middleware skips future DB checks
        setupService.MarkComplete();

        logger.LogInformation(
            "Initial setup completed. Admin account '{Username}' created.", user.Username);

        return Ok(new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt:   DateTimeOffset.UtcNow.AddMinutes(jwtOptions.Value.ExpiryMinutes),
            Username:    user.Username,
            Roles:       user.Roles));
    }
}
