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
        var msiLaunched = false; // true when msiexec is running detached and still has the file open

        try
        {
            // ── 1. Download ──────────────────────────────────────────────
            logger.LogInformation("AgentInstaller: downloading to {Path}", tempFile);
            await using (var stream = await _http.GetStreamAsync(downloadUrl, ct))
            await using (var file   = File.Create(tempFile))
                await stream.CopyToAsync(file, ct);

            logger.LogInformation("AgentInstaller: download complete ({Bytes} bytes)",
                new FileInfo(tempFile).Length);

            // ── 2. Launch installer ──────────────────────────────────────
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows MSI: the installer itself stops and restarts the Windows Service.
                // We must NOT wait for it — msiexec issues `sc stop KungConnectAgent` mid-install,
                // which would kill the very process that is waiting on WaitForExitAsync, creating
                // a deadlock / missed-exit situation.
                // Strategy: launch msiexec detached (UseShellExecute=true), then stop the host
                // immediately so SCM sees a clean exit. After the MSI finishes it starts the
                // upgraded service automatically via its own ServiceInstall table entry.
                var logFile = $"{tempFile}.log";
                var psi = new ProcessStartInfo(
                    "msiexec.exe",
                    $"/i \"{tempFile}\" /quiet /norestart /l*v \"{logFile}\"")
                {
                    UseShellExecute = true,
                };

                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start msiexec.");

                logger.LogInformation(
                    "AgentInstaller: msiexec launched (pid {Pid}). Stopping service — MSI will restart it when done.",
                    proc.Id);

                // Signal the finally block NOT to delete the temp file — msiexec is still reading it.
                // (The finally runs almost immediately after return, before msiexec has had time to
                // open the file, so an unconditional delete would silently kill the install.)
                // The file sits in the system temp folder and will be cleaned up eventually.
                msiLaunched = true;

                lifetime.StopApplication();
                return;
            }

            // ── Non-Windows: wait for installer, then stop ───────────────
            // dpkg / installer do NOT stop-and-restart the service themselves on Linux/macOS,
            // so we wait for the package tool to finish before handing control back to the SCM.
            var (exe, args) = BuildInstallerCommand(tempFile);
            logger.LogInformation("AgentInstaller: running {Exe} {Args}", exe, args);

            var nonWinPsi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            using var nonWinProc = Process.Start(nonWinPsi)
                ?? throw new InvalidOperationException("Failed to start installer process.");

            var stdout = await nonWinProc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await nonWinProc.StandardError.ReadToEndAsync(ct);
            await nonWinProc.WaitForExitAsync(ct);

            if (nonWinProc.ExitCode != 0)
            {
                logger.LogError(
                    "AgentInstaller: installer exited with code {Code}.\nstdout: {Out}\nstderr: {Err}",
                    nonWinProc.ExitCode, stdout, stderr);
                return;
            }

            logger.LogInformation("AgentInstaller: installer succeeded. Stopping service for restart.");
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
            // Skip cleanup when msiexec was launched detached — it still has the file open.
            // Any other failure path (download error, non-Windows) should clean up.
            if (!msiLaunched)
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (string exe, string args) BuildInstallerCommand(string installerPath)
    {
        // Windows is handled separately in InstallAsync (detached msiexec, no wait).
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
