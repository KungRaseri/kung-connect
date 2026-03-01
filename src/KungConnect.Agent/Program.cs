using KungConnect.Agent;
using KungConnect.Agent.Configuration;
using KungConnect.Agent.Services;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection(AgentOptions.Section));

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISignalingClientService, SignalingClientService>();
builder.Services.AddSingleton<SessionHandlerService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

