using KungConnect.Agent.Configuration;
using KungConnect.Shared.Constants;
using KungConnect.Shared.Signaling;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace KungConnect.Agent.Services;

/// <summary>
/// Manages the persistent SignalR connection to the KungConnect Server.
/// Handles reconnection, authentication, and incoming signaling messages.
/// </summary>
public interface ISignalingClientService : IAsyncDisposable
{
    HubConnection Connection { get; }
    bool IsConnected { get; }

    /// <summary>Opens the SignalR connection. Does NOT register the agent — call <see cref="RegisterAsync"/> next.</summary>
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Self-enrolls using the registration token. Returns the machine secret to persist.</summary>
    Task<string> EnrollAsync(string registrationToken, CancellationToken ct = default);

    /// <summary>Registers (or re-registers after reconnect) using the stored machine secret.</summary>
    Task RegisterAsync(CancellationToken ct = default);
}

public class SignalingClientService(
    IOptions<AgentOptions> agentOptions,
    ILogger<SignalingClientService> logger) : ISignalingClientService
{
    private readonly AgentOptions _opts = agentOptions.Value;
    private HubConnection? _connection;

    public HubConnection Connection => _connection
        ?? throw new InvalidOperationException("SignalR connection not yet started.");

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct = default)
    {
        var hubUrl = $"{_opts.ServerUrl.TrimEnd('/')}/hubs/signaling";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _connection.Reconnecting  += ex => { logger.LogWarning("SignalR reconnecting: {Msg}", ex?.Message); return Task.CompletedTask; };
        _connection.Reconnected   += _  => { logger.LogInformation("SignalR reconnected — re-registering"); return RegisterAsync(); };
        _connection.Closed        += ex => { logger.LogWarning("SignalR closed: {Msg}", ex?.Message); return Task.CompletedTask; };

        await _connection.StartAsync(ct);
        logger.LogInformation("SignalR connected to {Url}", hubUrl);
        // Caller (Worker) is responsible for calling EnrollAsync or RegisterAsync
    }

    /// <inheritdoc/>
    public async Task<string> EnrollAsync(string registrationToken, CancellationToken ct = default)
    {
        if (_connection is null) throw new InvalidOperationException("Call StartAsync first.");

        var hostname = string.IsNullOrWhiteSpace(_opts.MachineAlias)
            ? Environment.MachineName
            : _opts.MachineAlias;

        var osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
                   : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? "MacOs"
                   : "Linux";

        var version = typeof(SignalingClientService).Assembly
            .GetName().Version?.ToString(3) ?? "0.0.0";

        var secret = await _connection.InvokeAsync<string>(
            SignalingEvents.AgentEnroll, registrationToken, hostname, hostname, osType, version, ct);

        logger.LogInformation(
            "Enrolled: host={Host}, os={Os}, version={Ver}", hostname, osType, version);
        return secret;
    }

    /// <inheritdoc/>
    public async Task RegisterAsync(CancellationToken ct = default)
    {
        if (_connection is null) return;
        if (string.IsNullOrWhiteSpace(_opts.MachineSecret)) return; // not yet enrolled

        var hostname = string.IsNullOrWhiteSpace(_opts.MachineAlias)
            ? Environment.MachineName
            : _opts.MachineAlias;

        var osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
                   : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? "MacOs"
                   : "Linux";

        var version = typeof(SignalingClientService).Assembly
            .GetName().Version?.ToString(3) ?? "0.0.0";

        await _connection.InvokeAsync(
            SignalingEvents.AgentRegister, _opts.MachineSecret, hostname, osType, version, ct);
        logger.LogInformation(
            "Agent registered: host={Host}, os={Os}, version={Ver}", hostname, osType, version);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
            await _connection.StopAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
