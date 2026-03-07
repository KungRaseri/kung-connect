using KungConnect.Agent.Configuration;
using KungConnect.Shared.Constants;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace KungConnect.Agent.Services;

/// <summary>
/// Background service that polls GitHub Releases to detect newer agent versions.
/// When a newer release is found it notifies the server via the SignalR hub so the
/// dashboard can surface an "update available" badge on the machine card.
///
/// Enabled by setting <c>Agent__GitHubOwner</c> and <c>Agent__GitHubRepo</c> in
/// configuration (or appsettings.json).  Polling interval defaults to every 4 hours.
/// </summary>
public sealed class UpdateCheckerService(
    ISignalingClientService signalingClient,
    AgentConnectionStatus agentStatus,
    IOptions<AgentOptions> agentOptions,
    ILogger<UpdateCheckerService> logger) : BackgroundService
{
    private readonly AgentOptions _opts = agentOptions.Value;

    // GitHub API requires a User-Agent header; reuse a single client for the lifetime of the service.
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    static UpdateCheckerService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "KungConnect-Agent/1.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_opts.GitHubOwner) || string.IsNullOrWhiteSpace(_opts.GitHubRepo))
        {
            logger.LogDebug("UpdateChecker: GitHubOwner/GitHubRepo not configured — update checking disabled.");
            return;
        }

        // Wait on startup so the hub connection is established before the first
        // check tries to invoke a hub method.  30 s is enough for normal startup.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        var intervalHours = Math.Max(1, _opts.UpdateCheckIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckForUpdateAsync(stoppingToken);

            try { await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckForUpdateAsync(CancellationToken ct)
    {
        try
        {
            var url = $"https://api.github.com/repos/{_opts.GitHubOwner}/{_opts.GitHubRepo}/releases/latest";
            logger.LogDebug("UpdateChecker: polling {Url}", url);

            var release = await _http.GetFromJsonAsync<GitHubRelease>(url, ct);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return;

            // Strip leading 'v' so "v1.2.3" → "1.2.3" parses correctly.
            var tagWithoutV = release.TagName.TrimStart('v');
            if (!Version.TryParse(tagWithoutV, out var latestVersion))
            {
                logger.LogWarning("UpdateChecker: could not parse version from tag '{Tag}'", release.TagName);
                return;
            }

            // Compare against the currently running assembly version.
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

            if (latestVersion <= current)
            {
                logger.LogDebug("UpdateChecker: agent is up-to-date (current={Current}, latest={Latest})",
                    current.ToString(3), latestVersion.ToString(3));
                return;
            }

            logger.LogInformation(
                "UpdateChecker: new release found — {Current} → {Latest} ({Url})",
                current.ToString(3), latestVersion.ToString(3), release.HtmlUrl);

            // Surface the update in the system tray immediately, even if the hub is not yet
            // connected.  The tray polls AgentConnectionStatus every second.
            agentStatus.UpdateAvailableVersion = latestVersion.ToString(3);
            agentStatus.UpdateAvailableUrl     = release.HtmlUrl ?? string.Empty;

            if (!signalingClient.IsConnected)
            {
                logger.LogDebug("UpdateChecker: hub not connected; will retry on next poll cycle.");
                return;
            }

            // Notify the server so it can persist the info and push it to dashboard clients.
            await signalingClient.Connection.InvokeAsync(
                SignalingEvents.AgentUpdateAvailable,
                _opts.MachineSecret,
                latestVersion.ToString(3),
                release.HtmlUrl ?? string.Empty,
                ct);

            logger.LogInformation("UpdateChecker: server notified of update to v{Version}", latestVersion.ToString(3));
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("UpdateChecker: GitHub API request failed — {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "UpdateChecker: unexpected error during update check");
        }
    }

    // ── GitHub API response shape ────────────────────────────────────────────

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")]    string?          TagName,
        [property: JsonPropertyName("html_url")]    string?          HtmlUrl,
        [property: JsonPropertyName("name")]        string?          Name,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt);
}
