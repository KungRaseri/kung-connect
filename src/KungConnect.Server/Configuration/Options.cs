namespace KungConnect.Server.Configuration;

public class ServerOptions
{
    public const string Section = "Server";

    /// <summary>
    /// "SingleTenant" (default) – one org, simple auth.
    /// "MultiTenant"  – multiple orgs; requires tenant isolation middleware.
    /// </summary>
    public string Mode { get; set; } = "SingleTenant";

    public bool AllowSelfRegistration { get; set; } = false;
}

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "KungConnect";
    public string Audience { get; set; } = "KungConnect";
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}

public class RelayOptions
{
    public const string Section = "Relay";

    /// <summary>Whether to run the built-in TURN relay.</summary>
    public bool Enabled { get; set; } = false;
    public string TurnServerUrl { get; set; } = string.Empty;
    public string TurnUsername { get; set; } = string.Empty;
    public string TurnPassword { get; set; } = string.Empty;
    /// <summary>Shared secret for TURN credential HMAC generation.</summary>
    public string TurnSharedSecret { get; set; } = string.Empty;
}

public class RedisOptions
{
    public const string Section = "Redis";

    public bool Enabled { get; set; } = false;
    public string ConnectionString { get; set; } = "localhost:6379";
}

public class DownloadsOptions
{
    public const string Section = "Downloads";

    private const string Base = "https://github.com/KungRaseri/kung-connect/releases/latest/download";

    // Backing fields hold the hardcoded defaults.
    // Setters ignore empty/whitespace so that blank env-var overrides (e.g. Downloads__WindowsUrl=)
    // never clobber the defaults — only a non-empty value takes effect.
    private string _windowsUrl         = $"{Base}/KungConnect-win-x64.zip";
    private string _macOsUrl           = $"{Base}/KungConnect-osx-arm64.zip";
    private string _linuxUrl           = $"{Base}/KungConnect-linux-x64.tar.gz";
    private string _agentWindowsUrl    = $"{Base}/KungConnect-Agent-win-x64.msi";
    private string _agentMacOsUrl      = $"{Base}/KungConnect-Agent-osx-arm64.pkg";
    private string _agentLinuxUrl      = $"{Base}/KungConnect-Agent-linux-x64.deb";
    private string _agentLinuxArm64Url = $"{Base}/KungConnect-Agent-linux-arm64.deb";

    // ── Desktop client ──────────────────────────────────────────────────────
    public string WindowsUrl { get => _windowsUrl; set { if (!string.IsNullOrWhiteSpace(value)) _windowsUrl = value; } }
    public string MacOsUrl   { get => _macOsUrl;   set { if (!string.IsNullOrWhiteSpace(value)) _macOsUrl   = value; } }
    public string LinuxUrl   { get => _linuxUrl;   set { if (!string.IsNullOrWhiteSpace(value)) _linuxUrl   = value; } }

    // ── Agent ───────────────────────────────────────────────────────────────
    public string AgentWindowsUrl    { get => _agentWindowsUrl;    set { if (!string.IsNullOrWhiteSpace(value)) _agentWindowsUrl    = value; } }
    public string AgentMacOsUrl      { get => _agentMacOsUrl;      set { if (!string.IsNullOrWhiteSpace(value)) _agentMacOsUrl      = value; } }
    public string AgentLinuxUrl      { get => _agentLinuxUrl;      set { if (!string.IsNullOrWhiteSpace(value)) _agentLinuxUrl      = value; } }
    public string AgentLinuxArm64Url { get => _agentLinuxArm64Url; set { if (!string.IsNullOrWhiteSpace(value)) _agentLinuxArm64Url = value; } }
}
