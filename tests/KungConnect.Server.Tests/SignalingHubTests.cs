using KungConnect.Server.Data;
using KungConnect.Server.Hubs;
using KungConnect.Server.Services;
using KungConnect.Shared.Constants;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KungConnect.Server.Tests;

/// <summary>
/// Unit tests for <see cref="SignalingHub"/> using mocked hub infrastructure.
/// These tests verify routing logic without starting a real ASP.NET server.
/// </summary>
public sealed class SignalingHubTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// Builds a <see cref="SignalingHub"/> wired to mocked hub infrastructure.
    /// Returns the hub, the mock hub caller client, and a mock Clients collection.
    /// </summary>
    private static (SignalingHub hub, Mock<IHubCallerClients> clientsMock, Mock<ISingleClientProxy> callerProxy)
        CreateHub(AppDbContext db, string connectionId = "op-conn-1")
    {
        var joinCodeSvc   = new JoinCodeService(db, NullLogger<JoinCodeService>.Instance);
        var machineReg    = new Mock<IMachineRegistry>();
        var callerProxy   = new Mock<ISingleClientProxy>();
        var clientsMock   = new Mock<IHubCallerClients>();

        clientsMock.Setup(c => c.Caller).Returns(callerProxy.Object);
        clientsMock.Setup(c => c.Client(It.IsAny<string>()))
                   .Returns(new Mock<ISingleClientProxy>().Object);

        var hub = new SignalingHub(db, machineReg.Object, joinCodeSvc,
            new UpdateCheckStatusCache(),
            NullLogger<SignalingHub>.Instance);

        // Inject the hub context
        var ctx = new Mock<HubCallerContext>();
        ctx.Setup(c => c.ConnectionId).Returns(connectionId);
        hub.Context = ctx.Object;
        hub.Clients = clientsMock.Object;

        var groups = new Mock<IGroupManager>();
        hub.Groups = groups.Object;

        return (hub, clientsMock, callerProxy);
    }

    // ── JoinCodeCreate ─────────────────────────────────────────────────────

    [Fact]
    public async Task JoinCodeCreate_NoCode_CreatesCodeAndFiresJoinCodeCreated()
    {
        using var db = CreateDb();
        var (hub, _, callerProxy) = CreateHub(db);

        await hub.JoinCodeCreate(null);

        // Server should have sent JoinCodeCreated back to the caller
        callerProxy.Verify(c => c.SendCoreAsync(
            SignalingEvents.JoinCodeCreated,
            It.Is<object?[]>(args => args.Length == 2),
            default), Times.Once);
    }

    [Fact]
    public async Task JoinCodeCreate_WithCode_UsesSuppliedCode()
    {
        using var db = CreateDb();
        var (hub, _, callerProxy) = CreateHub(db);

        await hub.JoinCodeCreate("555444");

        callerProxy.Verify(c => c.SendCoreAsync(
            SignalingEvents.JoinCodeCreated,
            It.Is<object?[]>(args => args[0] != null && args[0]!.ToString() == "555444"),
            default), Times.Once);
    }

    [Fact]
    public async Task JoinCodeCreate_OperatorAlreadyWaiting_FiresOperatorJoinedToCaller()
    {
        using var db = CreateDb();
        var joinCodeSvc = new JoinCodeService(db, NullLogger<JoinCodeService>.Instance);
        // Pre-register an operator with code 123456
        await joinCodeSvc.CreateForOperatorAsync("123456", "operator-conn");

        // Now set up hub for the customer arriving with that code
        var callerProxy = new Mock<ISingleClientProxy>();
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Caller).Returns(callerProxy.Object);
        clientsMock.Setup(c => c.Client(It.IsAny<string>()))
                   .Returns(new Mock<ISingleClientProxy>().Object);

        var hub = new SignalingHub(db, new Mock<IMachineRegistry>().Object, joinCodeSvc,
            new UpdateCheckStatusCache(),
            NullLogger<SignalingHub>.Instance);

        var ctx = new Mock<HubCallerContext>();
        ctx.Setup(c => c.ConnectionId).Returns("customer-conn");
        hub.Context = ctx.Object;
        hub.Clients = clientsMock.Object;
        hub.Groups = new Mock<IGroupManager>().Object;

        await hub.JoinCodeCreate("123456");

        // Caller (customer) should get OperatorJoined
        callerProxy.Verify(c => c.SendCoreAsync(
            SignalingEvents.OperatorJoined,
            It.Is<object?[]>(args => args.Length > 0 && args[0]!.ToString() == "operator-conn"),
            default), Times.Once);
    }

    // ── JoinCodeConnect ────────────────────────────────────────────────────

    [Fact]
    public async Task JoinCodeConnect_InvalidCode_SendsError()
    {
        using var db = CreateDb();
        var (hub, _, callerProxy) = CreateHub(db, "op-conn");

        // "aaaaaa" is not 6 digits, will fail CreateForOperatorAsync
        await hub.JoinCodeConnect("aaaaaa");

        callerProxy.Verify(c => c.SendCoreAsync(
            SignalingEvents.Error,
            It.IsAny<object?[]>(),
            default), Times.Once);
    }

    [Fact]
    public async Task JoinCodeConnect_ValidCode_CustomerNotYetConnected_StoresPendingRecord()
    {
        using var db = CreateDb();
        var (hub, _, callerProxy) = CreateHub(db, "op-conn-999");

        await hub.JoinCodeConnect("888777");

        // Should have sent JoinCodeCreated back (confirmation to operator)
        callerProxy.Verify(c => c.SendCoreAsync(
            SignalingEvents.JoinCodeCreated,
            It.IsAny<object?[]>(),
            default), Times.Once);

        // And the pending record must exist in the database
        var pending = await db.JoinCodes.FirstOrDefaultAsync(j => j.Code == "888777");
        Assert.NotNull(pending);
        Assert.Equal("op-conn-999", pending!.OperatorConnectionId);
    }
}
