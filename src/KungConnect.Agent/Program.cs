using KungConnect.Agent;
using KungConnect.Agent.Configuration;
using KungConnect.Agent.Services;
using KungConnect.Agent.Setup;
#if WINDOWS
using KungConnect.Agent.Tray;
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
        // ── First-run detection ───────────────────────────────────────────────
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

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
            // WinExe has no console by default — allocate one for the interactive wizard.
            NativeConsole.Alloc();
            try
            {
                AgentInstaller.RunAsync(settingsPath).GetAwaiter().GetResult();
            }
            finally
            {
                NativeConsole.Free();
            }
#else
            if (Console.IsInputRedirected)
            {
                // Running as a service / in a container — can't show interactive prompts.
                Console.Error.WriteLine("[KungConnect Agent] Agent.ServerUrl is not configured.");
                Console.Error.WriteLine("Set Agent__ServerUrl via environment variable or appsettings.json, then restart.");
                return;
            }
            AgentInstaller.RunAsync(settingsPath).GetAwaiter().GetResult();
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
        builder.Services.AddHostedService<Worker>();

        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

        var host = builder.Build();

#if WINDOWS
        // Start Worker in the background, then hand control to WinForms for the tray loop.
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
}

