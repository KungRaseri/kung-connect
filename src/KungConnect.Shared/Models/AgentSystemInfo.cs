namespace KungConnect.Shared.Models;

/// <summary>
/// Hardware, network, OS, and storage snapshot collected by the agent at connect-time.
/// Stored as JSON in MachineEntity.SystemInfoJson and returned in MachineDetailDto.
/// </summary>
public class AgentSystemInfo
{
    // ── Network ──────────────────────────────────────────────────────────────
    public string       PrimaryIpAddress { get; set; } = string.Empty;
    public List<string> AllIpAddresses   { get; set; } = [];
    public string       PrimaryMacAddress { get; set; } = string.Empty;

    // ── Hardware ─────────────────────────────────────────────────────────────
    public string CpuName      { get; set; } = string.Empty;
    public int    CpuCores     { get; set; }
    public long   TotalRamBytes { get; set; }

    // ── Operating System ─────────────────────────────────────────────────────
    public string OsDescription  { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;

    // ── System ───────────────────────────────────────────────────────────────
    public string Timezone      { get; set; } = string.Empty;
    public long   UptimeSeconds { get; set; }

    // ── Storage ──────────────────────────────────────────────────────────────
    public List<DiskInfo> Disks { get; set; } = [];
}

public class DiskInfo
{
    public string Name      { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public long   TotalBytes { get; set; }
    public long   FreeBytes  { get; set; }
}
