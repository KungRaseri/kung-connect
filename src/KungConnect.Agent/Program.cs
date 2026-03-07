using KungConnect.Agent;
using KungConnect.Agent.Configuration;
using KungConnect.Agent.Services;
using KungConnect.Agent.Setup;
#if WINDOWS
using KungConnect.Agent.Tray;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.Windows.Forms;
#endif

namespace KungConnect.Agent;

internal static class Program
{
#if WINDOWS
    [STAThread]
#endif
    static void Main(string[] args)
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        // ── Silent configure mode ─────────────────────────────────────────────────
        // Called by platform installers (MSI custom action, deb postinst, pkg postinstall):
        //   KungConnect.Agent --configure --server-url https://my-server.com
        // Writes appsettings.json and exits — does NOT start the host.
        if (args.Contains("--configure"))
        {
            var serverUrl    = GetArg(args, "--server-url") ?? "";
            var machineAlias = GetArg(args, "--machine-alias") ?? Environment.MachineName;
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                Console.Error.WriteLine("[KungConnect Agent] --configure requires --server-url <url>");
                Environment.Exit(1);
            }
            AgentInstaller.WriteSettings(settingsPath, serverUrl, machineAlias);
            Console.WriteLine($"[KungConnect Agent] Configuration written to {settingsPath}");
            return;
        }

        // ── Uninstall notify mode ─────────────────────────────────────────────────
        // Called by platform uninstallers (MSI CA_NotifyUninstall) just before files are removed:
        //   KungConnect.Agent --notify-uninstall
        // Reads appsettings.json, POSTs to the server to mark the machine Uninstalled, then exits.
        if (args.Contains("--notify-uninstall"))
        {
            AgentInstaller.NotifyUninstallAsync(settingsPath).GetAwaiter().GetResult();
            return;
        }

        // ── First-run detection ───────────────────────────────────────────────
        var preConfig = new ConfigurationBuilder()
            .AddJsonFile(settingsPath, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configuredUrl = preConfig["Agent:ServerUrl"] ?? "";
        var needsSetup    = string.IsNullOrWhiteSpace(configuredUrl)
                         || configuredUrl == AgentOptions.DefaultServerUrl;

        if (needsSetup)
        {
#if WINDOWS
            // Skip the interactive wizard when running headless as a Windows Service.
            if (!WindowsServiceHelpers.IsWindowsService())
            {
                NativeConsole.Alloc();
                try   { AgentInstaller.RunAsync(settingsPath).GetAwaiter().GetResult(); }
                finally { NativeConsole.Free(); }
            }
#else
            if (!Console.IsInputRedirected)
                AgentInstaller.RunAsync(settingsPath).GetAwaiter().GetResult();
            else
            {
                Console.Error.WriteLine("[KungConnect Agent] Agent.ServerUrl is not configured.");
                Console.Error.WriteLine("Set Agent__ServerUrl via environment variable or appsettings.json, then restart.");
                return;
            }
#endif
        }

        // ── Host ──────────────────────────────────────────────────────────────
        var agentStatus = new AgentConnectionStatus();
        var builder     = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<AgentOptions>(
            builder.Configuration.GetSection(AgentOptions.Section));

        builder.Services.AddSingleton(agentStatus);
        builder.Services.AddSingleton<ISignalingClientService, SignalingClientService>();
        builder.Services.AddSingleton<SessionHandlerService>();
        builder.Services.AddSingleton<UpdateCheckerService>();
        builder.Services.AddSingleton<AgentInstallerService>();
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckerService>());

        // Keep the host alive even if a connection error leaks out of the
        // Worker retry loop — the service will reconnect on the next attempt.
        builder.Services.Configure<HostOptions>(o =>
            o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

#if WINDOWS
        builder.Services.AddWindowsService(o => o.ServiceName = "KungConnect Agent");
#else
        builder.Services.AddSystemd();
#endif

        var host = builder.Build();

#if WINDOWS
        // When running as a Windows Service (Session 0) there is no desktop,
        // so skip WinForms entirely and let the host run its own lifetime loop.
        if (WindowsServiceHelpers.IsWindowsService())
        {
            host.Run();
            return;
        }

        // Interactive session — start in the background and show the tray icon.
        host.StartAsync().GetAwaiter().GetResult();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext(host, agentStatus));

        host.StopAsync().GetAwaiter().GetResult();
#else
        // Linux / macOS: run as a normal foreground process / daemon.
        host.Run();
#endif
    }

    /// <summary>Returns the value after <paramref name="name"/> in <paramref name="args"/>, or null.</summary>
    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}

