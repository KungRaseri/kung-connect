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
