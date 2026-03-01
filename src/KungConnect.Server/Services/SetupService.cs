using KungConnect.Server.Data;
using KungConnect.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace KungConnect.Server.Services;

public interface ISetupService
{
    /// <summary>Returns true when no admin user exists yet and the setup wizard must be completed.</summary>
    Task<bool> IsSetupRequiredAsync();

    /// <summary>Call after successful setup to skip further DB checks.</summary>
    void MarkComplete();
}

/// <summary>
/// Singleton service that tracks whether the initial server setup has been completed.
/// The result is cached in-memory after the first DB check so every request doesn't
/// hit the database.  Setup is considered complete once at least one admin user exists.
/// </summary>
public class SetupService(IServiceScopeFactory scopeFactory) : ISetupService
{
    // null = not yet checked; true = setup done; false = setup still needed
    private bool? _isComplete;

    public async Task<bool> IsSetupRequiredAsync()
    {
        if (_isComplete.HasValue)
            return !_isComplete.Value;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hasAdmin = await db.Users.AnyAsync(u => u.Roles.Contains(Roles.Admin));
        _isComplete = hasAdmin;
        return !hasAdmin;
    }

    public void MarkComplete() => _isComplete = true;
}
