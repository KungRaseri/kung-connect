namespace KungConnect.Agent.Configuration;

public class AgentOptions
{
    public const string Section = "Agent";

    public string ServerUrl { get; set; } = "https://localhost:5001";
    public string MachineAlias { get; set; } = Environment.MachineName;

    /// <summary>
    /// One-time registration token set by the server admin (Server__AgentRegistrationToken).
    /// Used on first run to self-enroll. Cleared from config after successful enrollment.
    /// </summary>
    public string RegistrationToken { get; set; } = string.Empty;

    /// <summary>
    /// Secret used to authenticate this agent with the server.
    /// Populated automatically after self-enrollment, or copy from the dashboard
    /// 'Add Machine' provisioning flow.
    /// </summary>
    public string MachineSecret { get; set; } = string.Empty;
    public bool AutoAcceptSessions { get; set; } = true;
    public int CaptureFrameRateTarget { get; set; } = 30;
    public int VideoBitrateKbps { get; set; } = 2000;
}
