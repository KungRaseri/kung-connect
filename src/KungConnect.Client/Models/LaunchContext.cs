namespace KungConnect.Client.Models;

public enum LaunchMode { Normal, Session, AdHoc, Join }

/// <summary>
/// Parsed representation of the <c>kungconnect://</c> URI the desktop client was
/// launched with.  The browser web-UI fires one of three URI forms:
/// <list type="bullet">
///   <item><c>kungconnect://session?server=&amp;machine=&amp;token=</c>  — operator → registered machine</item>
///   <item><c>kungconnect://adhoc?server=&amp;target=&amp;token=&amp;code=</c> — operator → ad-hoc customer</item>
///   <item><c>kungconnect://join?server=&amp;code=</c>                   — customer-side join</item>
/// </list>
/// </summary>
public sealed record LaunchContext(
    LaunchMode Mode,
    string?    ServerUrl,
    string?    AccessToken,
    Guid?      MachineId,
    string?    TargetConnectionId,
    string?    JoinCode)
{
    /// <summary>Normal desktop startup — show the login screen.</summary>
    public static readonly LaunchContext Normal =
        new(LaunchMode.Normal, null, null, null, null, null);

    /// <summary>
    /// Parses <paramref name="args"/> for a <c>kungconnect://</c> URI.
    /// Returns <see cref="Normal"/> when args are empty or the URI is not recognized.
    /// </summary>
    public static LaunchContext Parse(string[] args)
    {
        if (args.Length == 0) return Normal;

        var raw = args[0];
        if (!raw.StartsWith("kungconnect://", StringComparison.OrdinalIgnoreCase))
            return Normal;

        Uri uri;
        try { uri = new Uri(raw); }
        catch { return Normal; }

        var q = ParseQuery(uri.Query);

        q.TryGetValue("server", out var server);
        q.TryGetValue("token",  out var token);
        q.TryGetValue("code",   out var code);
        q.TryGetValue("target", out var target);

        return uri.Host.ToLowerInvariant() switch
        {
            "session" => new LaunchContext(
                LaunchMode.Session,
                server, token,
                q.TryGetValue("machine", out var m) && Guid.TryParse(m, out var mid) ? mid : null,
                null, null),

            "adhoc" => new LaunchContext(
                LaunchMode.AdHoc,
                server, token,
                null, target, code),

            "join" => new LaunchContext(
                LaunchMode.Join,
                server, null,
                null, null, code),

            _ => Normal
        };
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var q = query.TrimStart('?');
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) continue;
            var key = Uri.UnescapeDataString(pair[..idx]);
            var val = Uri.UnescapeDataString(pair[(idx + 1)..]);
            result[key] = val;
        }
        return result;
    }
}
