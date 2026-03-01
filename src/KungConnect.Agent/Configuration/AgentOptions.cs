namespace KungConnect.Agent.Configuration;

public class AgentOptions
{
    public const string Section = "Agent";

    public string ServerUrl { get; set; } = "https://localhost:5001";
    public string MachineAlias { get; set; } = Environment.MachineName;
    /// <summary>
    /// Secret used to authenticate this agent with the server.
    /// Generated on first run and stored in user secrets / appsettings.
    /// </summary>
    public string MachineSecret { get; set; } = string.Empty;
    public bool AutoAcceptSessions { get; set; } = false;
    public int CaptureFrameRateTarget { get; set; } = 30;
    public int VideoBitrateKbps { get; set; } = 2000;
}
