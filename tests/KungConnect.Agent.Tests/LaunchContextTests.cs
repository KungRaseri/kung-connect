using KungConnect.Client.Models;

namespace KungConnect.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="LaunchContext.Parse"/>.
/// These are pure logic tests — no IO, no DI, no async.
/// </summary>
public sealed class LaunchContextTests
{
    // ── Normal / edge cases ────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyArgs_ReturnsNormal()
    {
        var ctx = LaunchContext.Parse([]);
        Assert.Equal(LaunchMode.Normal, ctx.Mode);
    }

    [Fact]
    public void Parse_NonUriArg_ReturnsNormal()
    {
        var ctx = LaunchContext.Parse(["--help"]);
        Assert.Equal(LaunchMode.Normal, ctx.Mode);
    }

    [Fact]
    public void Parse_WrongScheme_ReturnsNormal()
    {
        var ctx = LaunchContext.Parse(["https://example.com"]);
        Assert.Equal(LaunchMode.Normal, ctx.Mode);
    }

    [Fact]
    public void Parse_UnknownHost_ReturnsNormal()
    {
        var ctx = LaunchContext.Parse(["kungconnect://unknown?server=http://localhost"]);
        Assert.Equal(LaunchMode.Normal, ctx.Mode);
    }

    // ── Session mode ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_SessionUri_ParsesAllFields()
    {
        var machineId = Guid.NewGuid();
        var uri = $"kungconnect://session?server=https%3A%2F%2Fapp.example.com&machine={machineId}&token=abc123";

        var ctx = LaunchContext.Parse([uri]);

        Assert.Equal(LaunchMode.Session, ctx.Mode);
        Assert.Equal("https://app.example.com", ctx.ServerUrl);
        Assert.Equal(machineId, ctx.MachineId);
        Assert.Equal("abc123", ctx.AccessToken);
        Assert.Null(ctx.TargetConnectionId);
        Assert.Null(ctx.JoinCode);
    }

    [Fact]
    public void Parse_SessionUri_InvalidMachineId_MachineIdIsNull()
    {
        var uri = "kungconnect://session?server=http://localhost&machine=not-a-guid&token=tok";

        var ctx = LaunchContext.Parse([uri]);

        Assert.Equal(LaunchMode.Session, ctx.Mode);
        Assert.Null(ctx.MachineId);
    }

    // ── Ad-hoc mode ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_AdHocUri_ParsesAllFields()
    {
        var uri = "kungconnect://adhoc?server=https%3A%2F%2Fapp.example.com&target=conn-xyz&token=mytoken&code=123456";

        var ctx = LaunchContext.Parse([uri]);

        Assert.Equal(LaunchMode.AdHoc, ctx.Mode);
        Assert.Equal("https://app.example.com", ctx.ServerUrl);
        Assert.Equal("conn-xyz", ctx.TargetConnectionId);
        Assert.Equal("mytoken", ctx.AccessToken);
        Assert.Equal("123456", ctx.JoinCode);
        Assert.Null(ctx.MachineId);
    }

    // ── Join mode ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_JoinUri_ParsesCodeAndServer()
    {
        var uri = "kungconnect://join?server=https%3A%2F%2Fapp.example.com&code=654321";

        var ctx = LaunchContext.Parse([uri]);

        Assert.Equal(LaunchMode.Join, ctx.Mode);
        Assert.Equal("https://app.example.com", ctx.ServerUrl);
        Assert.Equal("654321", ctx.JoinCode);
        Assert.Null(ctx.AccessToken);
        Assert.Null(ctx.MachineId);
    }

    [Fact]
    public void Parse_JoinUri_NoCode_CodeIsNull()
    {
        var uri = "kungconnect://join?server=http://localhost";

        var ctx = LaunchContext.Parse([uri]);

        Assert.Equal(LaunchMode.Join, ctx.Mode);
        Assert.Null(ctx.JoinCode);
    }

    // ── Case insensitivity ─────────────────────────────────────────────────

    [Fact]
    public void Parse_SchemeUpperCase_StillParsed()
    {
        var uri = "KungConnect://Session?server=http://localhost&machine=00000000-0000-0000-0000-000000000001&token=t";

        var ctx = LaunchContext.Parse([uri]);

        Assert.Equal(LaunchMode.Session, ctx.Mode);
    }
}
