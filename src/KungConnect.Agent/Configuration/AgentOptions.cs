namespace KungConnect.Agent.Configuration;

public class AgentOptions
{
    public const string Section = "Agent";

    /// <summary>Placeholder written to appsettings.json on first install — triggers the setup wizard.</summary>
    public const string DefaultServerUrl = "https://localhost:5001";

    public string ServerUrl { get; set; } = DefaultServerUrl;
    public string MachineAlias { get; set; } = Environment.MachineName;

    /// <summary>
    /// Unique identity secret for this agent.
    /// Auto-generated on first run and persisted to appsettings.json.
    /// Do not change after initial registration — the server uses this to identify the machine.
    /// </summary>
    public string MachineSecret { get; set; } = string.Empty;
    public bool AutoAcceptSessions { get; set; } = true;
    public int CaptureFrameRateTarget { get; set; } = 30;
    public int VideoBitrateKbps { get; set; } = 2000;

    // ── Update checking via GitHub Releases ──────────────────────────────────

    /// <summary>
    /// GitHub organisation or username that owns the release repository.
    /// Set via <c>Agent__GitHubOwner</c> environment variable or appsettings.json.
    /// Leave blank (default) to disable automatic update checking.
    /// Example: <c>my-org</c>
    /// </summary>
    public string GitHubOwner { get; set; } = string.Empty;

    /// <summary>
    /// GitHub repository name containing the releases to check.
    /// Example: <c>kung-connect</c>
    /// </summary>
    public string GitHubRepo { get; set; } = string.Empty;

    /// <summary>How often (in hours) to poll GitHub Releases for a new version. Minimum 1. Default 4.</summary>
    public int UpdateCheckIntervalHours { get; set; } = 4;
}
