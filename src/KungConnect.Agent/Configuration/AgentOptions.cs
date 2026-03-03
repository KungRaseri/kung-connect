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
}
