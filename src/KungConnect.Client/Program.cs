using System;
using System.IO;
using Avalonia;

namespace KungConnect.Client;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // When published as a single-file exe, .NET extracts native DLLs (libSkiaSharp, Angle, …)
        // to a temp directory at startup.  If the exe was downloaded from the internet Windows
        // stamps a Mark-of-the-Web (Zone.Identifier ADS) on it; the runtime then applies the
        // same restriction to the extracted DLLs, causing a DllNotFoundException before the
        // window opens.  Redirecting extraction to %APPDATA%\KungConnect\.runtime sidesteps
        // this — AppData is never MOTW-restricted regardless of where the exe came from.
        // This MUST be the very first thing in Main, before any Avalonia/SkiaSharp is touched.
        Environment.SetEnvironmentVariable(
            "DOTNET_BUNDLE_EXTRACT_BASE_DIR",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KungConnect", ".runtime"));

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
