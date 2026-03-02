using KungConnect.Server.Data;
using KungConnect.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KungConnect.Server.Tests;

/// <summary>
/// Unit tests for <see cref="JoinCodeService"/> using an in-memory EF Core database.
/// All tests are isolated — each creates its own DbContext instance.
/// </summary>
public sealed class JoinCodeServiceTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static JoinCodeService CreateService(AppDbContext db) =>
        new(db, NullLogger<JoinCodeService>.Instance);

    // ── CreateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_GeneratesCode_WhenNoneSupplied()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.CreateAsync("conn-1");

        Assert.NotNull(result.Code);
        Assert.Equal(6, result.Code.Length);
        Assert.True(result.Code.All(char.IsAsciiDigit));
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateAsync_UsesSuppliedCode_WhenValid()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.CreateAsync("conn-1", "123456");

        Assert.Equal("123456", result.Code);
    }

    [Fact]
    public async Task CreateAsync_IgnoresSuppliedCode_WhenInvalid()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        // 5 digits — invalid
        var result = await svc.CreateAsync("conn-1", "12345");

        Assert.NotEqual("12345", result.Code);
        Assert.Equal(6, result.Code.Length);
    }

    [Fact]
    public async Task CreateAsync_ReplacesExistingCode_ForSameConnection()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        await svc.CreateAsync("conn-1");
        await svc.CreateAsync("conn-1");

        // Only one active code should exist for this connection
        var count = await db.JoinCodes
            .CountAsync(j => j.TargetConnectionId == "conn-1" && !j.IsConnected);
        Assert.Equal(1, count);
    }

    // ── CreateForOperatorAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CreateForOperatorAsync_Succeeds_WithValidCode()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var entity = await svc.CreateForOperatorAsync("999888", "op-conn-1");

        Assert.NotNull(entity);
        Assert.Equal("999888", entity.Code);
        Assert.Equal("op-conn-1", entity.OperatorConnectionId);
        Assert.Equal(string.Empty, entity.TargetConnectionId);
        Assert.False(entity.IsConnected);
    }

    [Fact]
    public async Task CreateForOperatorAsync_ReturnsNull_ForInvalidCode()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var entity = await svc.CreateForOperatorAsync("abc", "op-conn-1");

        Assert.Null(entity);
    }

    // ── RedeemAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task RedeemAsync_MarksCodeAsConnected()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var created = await svc.CreateAsync("customer-conn");
        var redeemed = await svc.RedeemAsync(created.Code, "op-conn");

        Assert.NotNull(redeemed);
        Assert.True(redeemed!.IsConnected);
        Assert.Equal("op-conn", redeemed.OperatorConnectionId);
    }

    [Fact]
    public async Task RedeemAsync_ReturnsNull_ForNonExistentCode()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.RedeemAsync("000000", "op-conn");

        Assert.Null(result);
    }

    [Fact]
    public async Task RedeemAsync_ReturnsNull_ForAlreadyRedeemedCode()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var created = await svc.CreateAsync("cust-conn");
        await svc.RedeemAsync(created.Code, "op-conn-1");

        // Second redeem should fail (already marked connected)
        var result = await svc.RedeemAsync(created.Code, "op-conn-2");

        Assert.Null(result);
    }

    // ── GetPendingOperatorAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetPendingOperatorAsync_FindsPreRegisteredCode()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        await svc.CreateForOperatorAsync("777666", "op-conn");
        var result = await svc.GetPendingOperatorAsync("777666");

        Assert.NotNull(result);
        Assert.Equal("op-conn", result!.OperatorConnectionId);
    }

    [Fact]
    public async Task GetPendingOperatorAsync_ReturnsNull_ForCustomerCreatedCode()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        // Customer created a code (TargetConnectionId is set)
        var created = await svc.CreateAsync("cust-conn");

        // Should NOT appear as a pending operator code
        var result = await svc.GetPendingOperatorAsync(created.Code);

        Assert.Null(result);
    }

    // ── GenerateCode ───────────────────────────────────────────────────────

    [Fact]
    public void GenerateCode_Returns6DigitNumericString()
    {
        for (int i = 0; i < 20; i++)
        {
            var code = JoinCodeService.GenerateCode();
            Assert.Equal(6, code.Length);
            Assert.True(code.All(char.IsAsciiDigit));
            Assert.True(int.Parse(code) >= 100_000);
            Assert.True(int.Parse(code) <= 999_999);
        }
    }
}
