using System.Security.Cryptography;
using KungConnect.Server.Data;
using KungConnect.Server.Data.Entities;
using KungConnect.Shared.DTOs.Join;
using Microsoft.EntityFrameworkCore;

namespace KungConnect.Server.Services;

public interface IJoinCodeService
{
    Task<CreateJoinCodeResponse> CreateAsync(string targetConnectionId, string? suggestedCode = null, CancellationToken ct = default);
    /// <summary>
    /// Called by the admin side: pre-registers a code they generated so the server can match
    /// when the customer calls JoinCodeCreate with the same code.
    /// Returns the entity so the hub can send confirmation back to the admin.
    /// Returns null if the code is invalid.
    /// </summary>
    Task<JoinCodeEntity?> CreateForOperatorAsync(string code, string operatorConnectionId, CancellationToken ct = default);
    Task<JoinCodeEntity?> RedeemAsync(string code, string operatorConnectionId, CancellationToken ct = default);
    Task<JoinCodeEntity?> GetPendingOperatorAsync(string code, CancellationToken ct = default);
    Task ExpireAsync(string code, CancellationToken ct = default);
}

public class JoinCodeService(AppDbContext db, ILogger<JoinCodeService> logger) : IJoinCodeService
{
    private const int CodeExpiryMinutes = 10;

    public async Task<CreateJoinCodeResponse> CreateAsync(string targetConnectionId, string? suggestedCode = null, CancellationToken ct = default)
    {
        // Invalidate any existing code for this connection
        var existing = await db.JoinCodes
            .Where(j => j.TargetConnectionId == targetConnectionId && !j.IsConnected)
            .ToListAsync(ct);
        db.JoinCodes.RemoveRange(existing);

        // Use caller-supplied code if valid 6-digit number, otherwise generate one
        var code = (suggestedCode?.Length == 6 && suggestedCode.All(char.IsAsciiDigit))
            ? suggestedCode
            : GenerateCode();
        var entity = new JoinCodeEntity
        {
            Code = code,
            TargetConnectionId = targetConnectionId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(CodeExpiryMinutes)
        };

        db.JoinCodes.Add(entity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Join code {Code} created for connection {ConnId}", code, targetConnectionId);
        return new CreateJoinCodeResponse(code, entity.ExpiresAt);
    }

    /// <summary>
    /// Admin pre-registers a code. We store a "placeholder" entity with an empty TargetConnectionId.
    /// When the customer calls JoinCodeCreate with the same code, we complete the handshake.
    /// </summary>
    public async Task<JoinCodeEntity?> CreateForOperatorAsync(string code, string operatorConnectionId, CancellationToken ct = default)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)) return null;

        // Remove any existing placeholder for this operator
        var existing = await db.JoinCodes
            .Where(j => j.OperatorConnectionId == operatorConnectionId && !j.IsConnected)
            .ToListAsync(ct);
        db.JoinCodes.RemoveRange(existing);

        var entity = new JoinCodeEntity
        {
            Code = code,
            TargetConnectionId = string.Empty, // will be filled when customer connects
            OperatorConnectionId = operatorConnectionId,
            IsConnected = false,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(CodeExpiryMinutes)
        };
        db.JoinCodes.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Operator pre-registered code {Code} for connId {ConnId}", code, operatorConnectionId);
        return entity;
    }

    /// <summary>Returns a pending operator-registered record for this code (TargetConnectionId is empty).</summary>
    public Task<JoinCodeEntity?> GetPendingOperatorAsync(string code, CancellationToken ct = default) =>
        db.JoinCodes.FirstOrDefaultAsync(
            j => j.Code == code && !j.IsConnected && j.TargetConnectionId == string.Empty && j.ExpiresAt > DateTimeOffset.UtcNow, ct);

    public async Task<JoinCodeEntity?> RedeemAsync(string code, string operatorConnectionId, CancellationToken ct = default)
    {
        var entity = await db.JoinCodes
            .FirstOrDefaultAsync(j => j.Code == code && !j.IsConnected && j.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (entity is null)
        {
            logger.LogWarning("Join code {Code} not found or already used", code);
            return null;
        }

        entity.OperatorConnectionId = operatorConnectionId;
        entity.IsConnected = true;
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task ExpireAsync(string code, CancellationToken ct = default)
    {
        var entity = await db.JoinCodes.FirstOrDefaultAsync(j => j.Code == code, ct);
        if (entity is not null)
        {
            db.JoinCodes.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Generates a random 6-digit numeric code (100000–999999).
    /// Callers may override this with their own value via <see cref="CreateAsync(string,string?,CancellationToken)"/>.
    /// </summary>
    public static string GenerateCode()
    {
        Span<byte> buf = stackalloc byte[4];
        RandomNumberGenerator.Fill(buf);
        var n = (int)(BitConverter.ToUInt32(buf) % 900_000) + 100_000;
        return n.ToString();
    }
}
