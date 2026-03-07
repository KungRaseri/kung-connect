using System.Collections.Concurrent;

namespace KungConnect.Server.Services;

/// <summary>
/// Lightweight in-memory cache of the most recent update-check result per machine.
/// Written by the SignalR hub when an agent reports back; read by the machines API.
/// No persistence needed — the result is only meaningful while the dashboard is polling.
/// </summary>
public sealed class UpdateCheckStatusCache
{
    private readonly ConcurrentDictionary<Guid, string> _statuses = new();

    /// <summary>Store the latest result for a machine.</summary>
    public void Set(Guid machineId, string status) => _statuses[machineId] = status;

    /// <summary>Returns the latest result, or <c>null</c> if no result is cached.</summary>
    public string? Get(Guid machineId) => _statuses.GetValueOrDefault(machineId);

    /// <summary>
    /// Clears any cached result. Called before triggering a new check so the dashboard
    /// polling loop waits for a fresh response rather than reading a stale one.
    /// </summary>
    public void Clear(Guid machineId) => _statuses.TryRemove(machineId, out _);
}
