using KungConnect.Agent;
using KungConnect.Agent.Configuration;
using KungConnect.Agent.Services;
using KungConnect.Agent.Setup;
using KungConnect.Agent.Tray;
using System.Windows.Forms;

namespace KungConnect.Agent;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // ── First-run detection ───────────────────────────────────────────────
        // Read config before building the host so we can run the interactive setup
        // wizard if the server URL hasn't been configured yet.
        // The wizard writes appsettings.json; the host then picks up the new values.
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        var preConfig = new ConfigurationBuilder()
            .AddJsonFile(settingsPath, optional: true)
            .AddEnvironmentVariables()     // picks up Agent__ServerUrl etc.
            .Build();

        var configuredUrl = preConfig["Agent:ServerUrl"] ?? "";
        var needsSetup    = string.IsNullOrWhiteSpace(configuredUrl)
                         || configuredUrl == AgentOptions.DefaultServerUrl;

        if (needsSetup)
        {
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

        // Suppress the generic hosting messages that look like a web-server starting.
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

        var host = builder.Build();

        // Start Worker and all hosted services in the background.
        // StartAsync returns as soon as each IHostedService.StartAsync has been called.
        host.StartAsync().GetAwaiter().GetResult();

        // ── System tray ───────────────────────────────────────────────────────
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext(host, agentStatus));

        // Application.Run returned (user clicked Exit or host stopped itself).
        // Ensure the host is fully wound down before the process exits.
        host.StopAsync().GetAwaiter().GetResult();
    }
}

