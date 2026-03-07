using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using KungConnect.Shared.Models;

namespace KungConnect.Agent.Services;

/// <summary>
/// Collects hardware, network, OS, and storage information from the local machine.
/// All operations are best-effort — individual failures are silently swallowed so
/// a single unavailable data source never prevents the agent from connecting.
/// </summary>
public static class SystemInfoCollector
{
    public static async Task<AgentSystemInfo> CollectAsync()
    {
        var info = new AgentSystemInfo();

        // Network
        try { CollectNetwork(info); } catch { /* best-effort */ }

        // CPU
        info.CpuCores = Environment.ProcessorCount;
        try { info.CpuName = await GetCpuNameAsync(); } catch { }

        // RAM — TotalAvailableMemoryBytes returns physical RAM for an unconstrained process
        try { info.TotalRamBytes = (long)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; } catch { }

        // OS
        // RuntimeInformation.OSDescription returns the NT kernel version string
        // (e.g. "Microsoft Windows 10.0.26200"), which reports Windows 11 as "10.0"
        // because Microsoft kept the major version at 10 for Windows 11.
        // Replace with a proper human-readable name on Windows.
        info.OsDescription  = GetFriendlyOsDescription();
        info.OsArchitecture = RuntimeInformation.OSArchitecture.ToString();
        info.RuntimeVersion = Environment.Version.ToString(3);

        // System
        info.Timezone      = TimeZoneInfo.Local.Id;
        info.UptimeSeconds = Environment.TickCount64 / 1000L;

        // Storage
        try { CollectDisks(info); } catch { }

        return info;
    }

    // ── OS description ────────────────────────────────────────────────────────

    private static string GetFriendlyOsDescription()
    {
#if WINDOWS
        // On Windows, RuntimeInformation.OSDescription returns the NT kernel string
        // (e.g. "Microsoft Windows 10.0.26200"). Windows 11 kept the major.minor at
        // 10.0, so we detect it by build number (22000 = 21H2, 22621 = 22H2, 26100 = 24H2, …).
        var ver = Environment.OSVersion.Version;
        if (ver.Build >= 22000)
        {
            // Map well-known Windows 11 builds to their marketing names.
            var release = ver.Build switch
            {
                22000 => "21H2",
                22621 => "22H2",
                22631 => "23H2",
                26100 => "24H2",
                _     => null
            };
            return release is null
                ? $"Windows 11 (Build {ver.Build})"
                : $"Windows 11 {release} (Build {ver.Build})";
        }
        return RuntimeInformation.OSDescription;
#else
        return RuntimeInformation.OSDescription;
#endif
    }

    // ── Network ───────────────────────────────────────────────────────────────

    private static void CollectNetwork(AgentSystemInfo info)
    {
        var ifaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                     && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                     && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            // Prefer Ethernet over Wi-Fi over everything else
            .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0
                        : n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 2)
            .ThenBy(n => n.Name)
            .ToList();

        info.AllIpAddresses = ifaces
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .Distinct()
            .ToList();

        info.PrimaryIpAddress = info.AllIpAddresses.FirstOrDefault() ?? string.Empty;

        var primary = ifaces.FirstOrDefault();
        if (primary is not null)
        {
            var bytes = primary.GetPhysicalAddress()?.GetAddressBytes() ?? [];
            if (bytes.Length == 6)
                info.PrimaryMacAddress = string.Join(":", bytes.Select(b => b.ToString("X2")));
        }
    }

    // ── CPU ───────────────────────────────────────────────────────────────────

    private static Task<string> GetCpuNameAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Task.FromResult(GetWindowsCpuName());
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return GetLinuxCpuNameAsync();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RunAsync("sysctl", "-n machdep.cpu.brand_string");
        return Task.FromResult(string.Empty);
    }

    private static string GetWindowsCpuName()
    {
#if WINDOWS
        using var key = Microsoft.Win32.Registry.LocalMachine
            .OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return (key?.GetValue("ProcessorNameString") as string ?? string.Empty).Trim();
#else
        return string.Empty;
#endif
    }

    private static async Task<string> GetLinuxCpuNameAsync()
    {
        var cpuinfo = await File.ReadAllTextAsync("/proc/cpuinfo");
        var match   = Regex.Match(cpuinfo, @"model name\s*:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static async Task<string> RunAsync(string exe, string args)
    {
        using var proc = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            }
        };
        proc.Start();
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output.Trim();
    }

    // ── Storage ───────────────────────────────────────────────────────────────

    private static void CollectDisks(AgentSystemInfo info)
    {
        info.Disks = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DiskInfo
            {
                Name       = d.Name,
                DriveType  = d.DriveType.ToString(),
                TotalBytes = d.TotalSize,
                FreeBytes  = d.TotalFreeSpace
            })
            .ToList();
    }
}
