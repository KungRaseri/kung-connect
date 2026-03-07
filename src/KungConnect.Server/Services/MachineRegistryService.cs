using System.Collections.Concurrent;
using KungConnect.Server.Data;
using KungConnect.Server.Data.Entities;
using KungConnect.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace KungConnect.Server.Services;

/// <summary>
/// Tracks which machines are currently online and their SignalR connection IDs.
/// An in-memory layer sits on top of the database for low-latency lookups.
/// For multi-node deployments, replace the ConcurrentDictionary with Redis.
/// </summary>
public interface IMachineRegistry
{
    Task<MachineEntity?> AuthenticateAsync(string machineSecret, CancellationToken ct = default);
    Task SetOnlineAsync(Guid machineId, string connectionId, CancellationToken ct = default);
    Task SetOfflineAsync(string connectionId, CancellationToken ct = default);
    Task<string?> GetConnectionIdAsync(Guid machineId, CancellationToken ct = default);
    Task<MachineEntity?> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default);
}

public class MachineRegistry(IServiceScopeFactory scopeFactory, ILogger<MachineRegistry> logger)
    : IMachineRegistry
{
    // connectionId → machineId
    private readonly ConcurrentDictionary<string, Guid> _connToMachine = new();
    // machineId → connectionId
    private readonly ConcurrentDictionary<Guid, string> _machineToConn = new();

    public async Task<MachineEntity?> AuthenticateAsync(string machineSecret, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Machines.FirstOrDefaultAsync(m => m.MachineSecret == machineSecret, ct);
    }

    public async Task SetOnlineAsync(Guid machineId, string connectionId, CancellationToken ct = default)
    {
        _machineToConn[machineId] = connectionId;
        _connToMachine[connectionId] = machineId;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var machine = await db.Machines.FindAsync([machineId], ct);
        if (machine is null) return;

        machine.Status = MachineStatus.Online;
        machine.SignalRConnectionId = connectionId;
        machine.LastSeen = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Machine {Id} ({Alias}) is now online", machineId, machine.Alias);
    }

    public async Task SetOfflineAsync(string connectionId, CancellationToken ct = default)
    {
        if (!_connToMachine.TryRemove(connectionId, out var machineId)) return;
        _machineToConn.TryRemove(machineId, out _);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var machine = await db.Machines.FindAsync([machineId], ct);
        if (machine is null) return;

        machine.SignalRConnectionId = null;
        machine.LastSeen = DateTimeOffset.UtcNow;

        // Don't downgrade from Uninstalled — the uninstall CA already set the final status.
        if (machine.Status != MachineStatus.Uninstalled)
            machine.Status = MachineStatus.Offline;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Machine {Id} ({Alias}) went offline", machineId, machine.Alias);
    }

    public Task<string?> GetConnectionIdAsync(Guid machineId, CancellationToken ct = default)
    {
        _machineToConn.TryGetValue(machineId, out var connId);
        return Task.FromResult(connId);
    }

    public async Task<MachineEntity?> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default)
    {
        if (!_connToMachine.TryGetValue(connectionId, out var machineId)) return null;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Machines.FindAsync([machineId], ct);
    }
}
