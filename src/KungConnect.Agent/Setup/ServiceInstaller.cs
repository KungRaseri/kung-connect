using System.Diagnostics;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Setup;

/// <summary>
/// Installs the agent as an OS background service so it starts automatically
/// without the user having to do anything after the initial wizard.
///
/// Platform behaviour:
///   Windows (elevated)  — Windows Service via sc.exe (starts at boot, survives logoff)
///   Windows (no UAC)    — HKCU Run registry key (starts at login)
///   Linux               — systemd user unit  (~/.config/systemd/user/)
///   macOS               — launchd LaunchAgent (~/Library/LaunchAgents/)
/// </summary>
internal static class ServiceInstaller
{
    private const string ServiceName    = "KungConnectAgent";
    private const string ServiceDisplay = "KungConnect Agent";
    private const string ServiceDesc    = "KungConnect remote-management agent";
    private const string LaunchAgentId  = "com.kungconnect.agent";

    public record InstallResult(bool Success, string Message);

    /// <summary>
    /// Installs and starts the service for the current platform.
    /// <paramref name="exePath"/> must be the fully-qualified path to this executable.
    /// </summary>
    public static async Task<InstallResult> InstallAsync(string exePath)
    {
#if WINDOWS
        return InstallWindows(exePath);
#else
        if (OperatingSystem.IsLinux())
            return await InstallLinuxAsync(exePath);
        if (OperatingSystem.IsMacOS())
            return await InstallMacOsAsync(exePath);
        return new InstallResult(false, "Unsupported platform — service installation skipped.");
#endif
    }

    // ── Windows ───────────────────────────────────────────────────────────────

#if WINDOWS
    [SupportedOSPlatform("windows")]
    private static InstallResult InstallWindows(string exePath)
    {
        bool elevated = new System.Security.Principal.WindowsPrincipal(
            System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

        return elevated
            ? InstallWindowsService(exePath)
            : InstallRegistryRunKey(exePath);
    }

    [SupportedOSPlatform("windows")]
    private static InstallResult InstallWindowsService(string exePath)
    {
        // Remove any previous installation first so re-running the wizard is safe.
        Run("sc", $"stop \"{ServiceName}\"");
        Run("sc", $"delete \"{ServiceName}\"");

        bool ok = Run("sc", $"create \"{ServiceName}\" binPath= \"{exePath}\" start= auto DisplayName= \"{ServiceDisplay}\"")
               && Run("sc", $"description \"{ServiceName}\" \"{ServiceDesc}\"")
               && Run("sc", $"start \"{ServiceName}\"");

        return ok
            ? new InstallResult(true,  $"Windows Service '{ServiceDisplay}' installed and started.")
            : new InstallResult(false, $"sc.exe failed — check the Windows Event Log for details.");
    }

    [SupportedOSPlatform("windows")]
    private static InstallResult InstallRegistryRunKey(string exePath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key is null)
                return new InstallResult(false, "Could not open registry Run key.");

            key.SetValue(ServiceDisplay, $"\"{exePath}\"");
            return new InstallResult(true,
                "Registered in HKCU\\Run — the agent will start automatically when you log in.\n" +
                "  Tip: run as Administrator to install a Windows Service that starts at boot.");
        }
        catch (Exception ex)
        {
            return new InstallResult(false, $"Registry write failed: {ex.Message}");
        }
    }
#endif

    // ── Linux (systemd user service) ─────────────────────────────────────────

    private static async Task<InstallResult> InstallLinuxAsync(string exePath)
    {
        try
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                          ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            var unitDir  = Path.Combine(configHome, "systemd", "user");
            Directory.CreateDirectory(unitDir);

            var unitPath = Path.Combine(unitDir, "kungconnect-agent.service");
            await File.WriteAllTextAsync(unitPath,
                $"""
                [Unit]
                Description={ServiceDesc}
                After=network-online.target
                Wants=network-online.target

                [Service]
                Type=notify
                ExecStart={exePath}
                Restart=on-failure
                RestartSec=10

                [Install]
                WantedBy=default.target
                """);

            // Make the binary executable (important when freshly extracted from a zip/tar).
            Run("chmod", $"+x \"{exePath}\"");

            bool ok = Run("systemctl", "--user daemon-reload")
                   && Run("systemctl", "--user enable --now kungconnect-agent");

            return ok
                ? new InstallResult(true,  "Registered as a systemd user service (starts at login).")
                : new InstallResult(false, "systemctl failed — run: systemctl --user status kungconnect-agent");
        }
        catch (Exception ex)
        {
            return new InstallResult(false, $"Linux service install failed: {ex.Message}");
        }
    }

    // ── macOS (launchd LaunchAgent) ───────────────────────────────────────────

    private static async Task<InstallResult> InstallMacOsAsync(string exePath)
    {
        try
        {
            var launchAgentsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "LaunchAgents");
            Directory.CreateDirectory(launchAgentsDir);

            var logPath   = Path.Combine(Path.GetDirectoryName(exePath)!, "agent.log");
            var plistPath = Path.Combine(launchAgentsDir, $"{LaunchAgentId}.plist");
            await File.WriteAllTextAsync(plistPath,
                $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
                    "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>            <string>{LaunchAgentId}</string>
                    <key>ProgramArguments</key> <array><string>{exePath}</string></array>
                    <key>RunAtLoad</key>        <true/>
                    <key>KeepAlive</key>        <true/>
                    <key>StandardOutPath</key>  <string>{logPath}</string>
                    <key>StandardErrorPath</key><string>{logPath}</string>
                </dict>
                </plist>
                """);

            Run("chmod", $"+x \"{exePath}\"");

            // Unload any previous version first so re-running the wizard is safe.
            Run("launchctl", $"unload \"{plistPath}\"");
            bool ok = Run("launchctl", $"load \"{plistPath}\"");

            return ok
                ? new InstallResult(true,  "Registered as a launchd LaunchAgent (starts at login).")
                : new InstallResult(false, $"launchctl failed — check {logPath}");
        }
        catch (Exception ex)
        {
            return new InstallResult(false, $"macOS service install failed: {ex.Message}");
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>Runs a command, returns true if exit code is 0.</summary>
    private static bool Run(string cmd, string args)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(cmd, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            })!;
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}
