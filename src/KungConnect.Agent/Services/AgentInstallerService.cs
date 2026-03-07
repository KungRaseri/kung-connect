using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;

namespace KungConnect.Agent.Services;

/// <summary>
/// Downloads and silently installs an agent update, then signals the host to stop
/// so the service manager (SCM / systemd) can restart with the new binary.
///
/// Platform behaviour:
/// <list type="bullet">
///   <item><b>Windows</b> — downloads the .msi and runs
///     <c>msiexec /i KungConnect-Agent-win-x64.msi /quiet /norestart</c>.
///     The MSI replaces the binaries while the service is still running, then the
///     service exits and SCM restarts it with the upgraded binary.</item>
///   <item><b>Linux (x64)</b> — downloads the .deb and runs
///     <c>dpkg --install --force-confnew *.deb</c> (requires root / sudo).</item>
///   <item><b>macOS</b> — downloads the .pkg and runs
///     <c>installer -pkg *.pkg -target /</c>.</item>
/// </list>
/// </summary>
public sealed class AgentInstallerService(
    IHostApplicationLifetime lifetime,
    ILogger<AgentInstallerService> logger)
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    static AgentInstallerService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "KungConnect-Agent/1.0");
    }

    /// <summary>
    /// Download <paramref name="downloadUrl"/>, run the installer silently, then
    /// stop the host so the service manager restarts the process with the new binary.
    /// All errors are logged; the method never throws.
    /// </summary>
    public async Task InstallAsync(string downloadUrl, CancellationToken ct = default)
    {
        logger.LogInformation("AgentInstaller: starting unattended update from {Url}", downloadUrl);

        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(downloadUrl));

        try
        {
            // ── 1. Download ──────────────────────────────────────────────
            logger.LogInformation("AgentInstaller: downloading to {Path}", tempFile);
            await using (var stream = await _http.GetStreamAsync(downloadUrl, ct))
            await using (var file   = File.Create(tempFile))
                await stream.CopyToAsync(file, ct);

            logger.LogInformation("AgentInstaller: download complete ({Bytes} bytes)",
                new FileInfo(tempFile).Length);

            // ── 2. Run installer silently ────────────────────────────────
            var (exe, args) = BuildInstallerCommand(tempFile);
            logger.LogInformation("AgentInstaller: running {Exe} {Args}", exe, args);

            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            // On Linux/macOS the installer may need elevated privileges.
            // When running as a systemd service the process typically runs as root already.
            // On Windows the SCM typically runs services as LocalSystem — msiexec can install.
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start installer process.");

            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                logger.LogError(
                    "AgentInstaller: installer exited with code {Code}.\nstdout: {Out}\nstderr: {Err}",
                    proc.ExitCode, stdout, stderr);
                return;
            }

            logger.LogInformation("AgentInstaller: installer succeeded. Stopping service for restart.");

            // ── 3. Stop the host — service manager will restart with new binary ──
            lifetime.StopApplication();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("AgentInstaller: cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentInstaller: unhandled error during install.");
        }
        finally
        {
            // Best-effort cleanup — the MSI/deb/pkg may still be locked by the installer.
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (string exe, string args) BuildInstallerCommand(string installerPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // /quiet     — no UI
            // /norestart — don't auto-reboot the machine; we'll handle the restart ourselves
            return ("msiexec.exe", $"/i \"{installerPath}\" /quiet /norestart /l*v \"{installerPath}.log\"");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // --force-confnew overwrites existing config files with package defaults
            return ("dpkg", $"--install --force-confnew \"{installerPath}\"");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ("installer", $"-pkg \"{installerPath}\" -target /");
        }

        throw new PlatformNotSupportedException("Unsupported OS for unattended agent install.");
    }
}
