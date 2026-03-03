using KungConnect.Agent;
using KungConnect.Agent.Configuration;
using KungConnect.Agent.Services;
using KungConnect.Agent.Setup;

// ── First-run detection ───────────────────────────────────────────────────────
// Read config before building the host so we can run the interactive setup
// wizard if the server URL hasn't been configured yet.
// The wizard writes appsettings.json; the host then picks up the new values.
var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

var preConfig = new ConfigurationBuilder()
    .AddJsonFile(settingsPath, optional: true)
    .AddEnvironmentVariables()        // picks up Agent__ServerUrl etc.
    .Build();

var configuredUrl = preConfig["Agent:ServerUrl"] ?? "";
var needsSetup    = string.IsNullOrWhiteSpace(configuredUrl)
                 || configuredUrl == AgentOptions.DefaultServerUrl;

if (needsSetup)
{
    if (Console.IsInputRedirected)
    {
        // Running as a service / in a container — can't show interactive prompts.
        Console.Error.WriteLine("[KungConnect Agent] Agent.ServerUrl is not configured.");
        Console.Error.WriteLine("Set Agent__ServerUrl via environment variable or appsettings.json, then restart.");
        return;
    }

    await AgentInstaller.RunAsync(settingsPath);
    // agentInstaller wrote appsettings.json — the host builder below will re-read it.
}

// ── Host ──────────────────────────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection(AgentOptions.Section));

builder.Services.AddSingleton<ISignalingClientService, SignalingClientService>();
builder.Services.AddSingleton<SessionHandlerService>();
builder.Services.AddHostedService<Worker>();

// Suppress the generic hosting messages that look like a web-server starting.
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

var host = builder.Build();
host.Run();

