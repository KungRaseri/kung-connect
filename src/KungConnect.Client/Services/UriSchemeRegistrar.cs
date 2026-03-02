using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KungConnect.Client.Services;

/// <summary>
/// Registers the <c>kungconnect://</c> custom URI scheme with the operating system
/// so that the browser can deep-link into the desktop client.
///
/// Called once on startup — safe to call every run (idempotent writes).
///
/// Platform behaviour:
/// <list type="bullet">
///   <item><b>Windows</b> — writes <c>HKCU\Software\Classes\kungconnect</c></item>
///   <item><b>Linux</b>   — writes <c>~/.local/share/applications/kungconnect.desktop</c>
///                          and calls <c>xdg-mime default</c></item>
///   <item><b>macOS</b>   — requires <c>CFBundleURLTypes</c> in the app bundle's
///                          <c>Info.plist</c>; nothing can be done at runtime</item>
/// </list>
/// </summary>
public static class UriSchemeRegistrar
{
    private const string Scheme = "kungconnect";

    public static void EnsureRegistered()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                RegisterWindows();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                RegisterLinux();
            // macOS: Info.plist CFBundleURLTypes must be set at build time — nothing to do here
        }
        catch (Exception ex)
        {
            // Non-fatal: the app still works; deep-link launching just won't work until
            // the scheme is registered (e.g. by an installer)
            Debug.WriteLine($"[UriSchemeRegistrar] Registration failed: {ex.Message}");
        }
    }

    // ── Windows ───────────────────────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows()
    {
        var exe = GetExecutablePath();
        if (exe is null) return;

#pragma warning disable CA1416 // Registry is Windows-only, guarded by [SupportedOSPlatform]
        using var root = Microsoft.Win32.Registry.CurrentUser
            .CreateSubKey($@"Software\Classes\{Scheme}");
        root.SetValue("", $"URL:{Scheme} Protocol");
        root.SetValue("URL Protocol", "");

        using var iconKey = root.CreateSubKey("DefaultIcon");
        iconKey.SetValue("", $"\"{exe}\",0");

        using var cmdKey = root.CreateSubKey(@"shell\open\command");
        cmdKey.SetValue("", $"\"{exe}\" \"%1\"");
#pragma warning restore CA1416
    }

    // ── Linux ─────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("linux")]
    private static void RegisterLinux()
    {
        var exe = GetExecutablePath();
        if (exe is null) return;

        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications");
        Directory.CreateDirectory(appDir);

        var desktopFile = Path.Combine(appDir, "kungconnect.desktop");
        File.WriteAllText(desktopFile,
            $"""
            [Desktop Entry]
            Name=KungConnect
            Exec={exe} %u
            Type=Application
            NoDisplay=true
            MimeType=x-scheme-handler/{Scheme};
            """);

        // Inform the MIME database — ignore errors if xdg-mime is unavailable
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName        = "xdg-mime",
                Arguments       = $"default kungconnect.desktop x-scheme-handler/{Scheme}",
                UseShellExecute = false,
                CreateNoWindow  = true,
            });
            proc?.WaitForExit(3_000);
        }
        catch { /* xdg-mime not present */ }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string? GetExecutablePath()
        => Process.GetCurrentProcess().MainModule?.FileName;
}
