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

    // Completed by TriggerNow() to wake the poll-wait early.
    private TaskCompletionSource _trigger = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Wakes the background loop immediately so it performs an update check right now
    /// instead of waiting for the next scheduled interval.
    /// </summary>
    public void TriggerNow()
    {
        // TrySetResult is a no-op if already completed — safe to call from any thread.
        _trigger.TrySetResult();
    }

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
            // GitHub not configured: automatic checks are disabled, but keep the service alive
            // so on-demand TriggerNow() calls produce a clear warning instead of silently
            // doing nothing (which makes the dashboard show "Agent is up-to-date" incorrectly).
            logger.LogWarning(
                "UpdateChecker: GitHubOwner/GitHubRepo not set — automatic update checks disabled. "
              + "Set Agent__GitHubOwner and Agent__GitHubRepo in appsettings.json to enable.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.WhenAny(_trigger.Task, Task.Delay(Timeout.Infinite, stoppingToken)); }
                catch (OperationCanceledException) { return; }
                _trigger = new(TaskCreationOptions.RunContinuationsAsynchronously);
                logger.LogWarning(
                    "UpdateChecker: on-demand check requested but GitHub is not configured. "
                  + "Set Agent__GitHubOwner and Agent__GitHubRepo in appsettings.json.");
                if (signalingClient.IsConnected)
                {
                    try
                    {
                        await signalingClient.Connection.InvokeAsync(
                            SignalingEvents.AgentUpdateCheckStatus,
                            _opts.MachineSecret, "github-not-configured");
                    }
                    catch { /* best-effort */ }
                }
            }
            return;
        }

        // Wait on startup so the hub connection is established before the first check.
        // TriggerNow() can cut this delay short (e.g. when the dashboard requests a check).
        try
        {
            await Task.WhenAny(
                Task.Delay(TimeSpan.FromSeconds(30), stoppingToken),
                _trigger.Task);
            stoppingToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { return; }

        var intervalHours = Math.Max(1, _opts.UpdateCheckIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Reset trigger BEFORE checking so a TriggerNow() that arrives during the
            // check isn't silently dropped — it will cause the next iteration immediately.
            _trigger = new(TaskCreationOptions.RunContinuationsAsynchronously);

            await CheckForUpdateAsync(stoppingToken);

            try
            {
                await Task.WhenAny(
                    Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken),
                    _trigger.Task);
                stoppingToken.ThrowIfCancellationRequested();
            }
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

            // Strip leading 'v' and any pre-release suffix (e.g. "v0.0.58-abc1234" → "0.0.58").
            // .NET's Version class does not support SemVer pre-release labels; everything
            // after the first '-' must be removed before calling Version.TryParse.
            var tagWithoutV      = release.TagName.TrimStart('v');
            var tagVersionPart   = tagWithoutV.Split('-')[0];
            if (!Version.TryParse(tagVersionPart, out var latestVersion))
            {
                logger.LogWarning("UpdateChecker: could not parse version from tag '{Tag}'", release.TagName);
                return;
            }

            // Compare against the currently running assembly version.
            // Normalise both to 3 components: .NET assembly versions are 4-part (1.0.0.0, revision=0)
            // while GitHub tags are 3-part (v1.0.0, revision=-1).  Without normalisation
            // Version("1.0.0") < Version("1.0.0.0") in .NET's comparator, which would make
            // a matching GitHub release appear older than the installed version.
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            var latestNorm  = new Version(latestVersion.Major, latestVersion.Minor, Math.Max(0, latestVersion.Build));
            var currentNorm = new Version(current.Major,       current.Minor,       Math.Max(0, current.Build));

            if (latestNorm <= currentNorm)
            {
                logger.LogDebug("UpdateChecker: agent is up-to-date (current={Current}, latest={Latest})",
                    currentNorm, latestNorm);
                if (signalingClient.IsConnected)
                {
                    try
                    {
                        await signalingClient.Connection.InvokeAsync(
                            SignalingEvents.AgentUpdateCheckStatus,
                            _opts.MachineSecret, "up-to-date", ct);
                    }
                    catch { /* best-effort */ }
                }
                return;
            }

            logger.LogInformation(
                "UpdateChecker: new release found — {Current} → {Latest} ({Url})",
                currentNorm, latestNorm, release.HtmlUrl);

            // Surface the update in the system tray immediately, even if the hub is not yet
            // connected.  The tray polls AgentConnectionStatus every second.
            agentStatus.UpdateAvailableVersion = latestNorm.ToString();
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
                latestNorm.ToString(),
                release.HtmlUrl ?? string.Empty,
                ct);

            logger.LogInformation("UpdateChecker: server notified of update to v{Version}", latestNorm);
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
