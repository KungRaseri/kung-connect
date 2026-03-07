using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KungConnect.Client.Services;

/// <summary>
/// Checks GitHub Releases to detect whether a newer version of the desktop client
/// is available.  Safe to call from any thread; failures are silently ignored.
/// Skips the check on local/dev builds (version starts with "0.0.0").
///
/// <para>
/// To mark a release as <b>required</b> (forces an update before the client can
/// connect), include the literal text <c>[REQUIRED]</c> anywhere in the GitHub
/// release description body.  When <see cref="UpdateInfo.IsRequired"/> is true
/// the URI-launch flow will block and show a download prompt instead of
/// proceeding with the session.
/// </para>
/// </summary>
public static class ClientUpdateChecker
{
    private static readonly HttpClient _http;
    private const string ApiUrl =
        "https://api.github.com/repos/KungRaseri/kung-connect/releases/latest";

    static ClientUpdateChecker()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", "KungConnect-Client/1.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    /// <summary>The informational version string baked in at build time (e.g. "0.0.68").</summary>
    public static string CurrentVersion
    {
        get
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            return System.Diagnostics.FileVersionInfo
                       .GetVersionInfo(asm.Location).ProductVersion
                   ?? asm.GetName().Version?.ToString(3)
                   ?? "0.0.0";
        }
    }

    /// <param name="Version">Human-readable version string, e.g. "0.0.72".</param>
    /// <param name="Url">GitHub release page URL for the Download button.</param>
    /// <param name="IsRequired">
    ///   True when the release body contains <c>[REQUIRED]</c>.
    ///   The URI-launch flow should block session start and prompt for update.
    /// </param>
    public record UpdateInfo(string Version, string Url, bool IsRequired = false);

    /// <summary>
    /// Returns update info if a newer GitHub release exists, otherwise null.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var current = CurrentVersion;
            if (current.StartsWith("0.0.0")) return null;   // skip dev builds

            var release = await _http.GetFromJsonAsync<GitHubRelease>(ApiUrl, ct);
            if (release?.TagName is not { } tag) return null;

            // Strip the leading 'v' then compare only the numeric part
            var latestStr     = tag.TrimStart('v');
            var latestSemVer  = latestStr.Split('-')[0];
            var currentSemVer = current.Split('-')[0];

            if (!Version.TryParse(latestSemVer,  out var latest)) return null;
            if (!Version.TryParse(currentSemVer, out var curr))   return null;

            if (latest <= curr) return null;

            // A release is "required" when the author marks the body with [REQUIRED].
            // This is the hook for forced-update enforcement — currently informational only.
            var isRequired = release.Body?.Contains("[REQUIRED]",
                StringComparison.OrdinalIgnoreCase) ?? false;

            return new UpdateInfo(
                latestStr,
                release.HtmlUrl ?? "https://github.com/KungRaseri/kung-connect/releases/latest",
                isRequired);
        }
        catch
        {
            return null;
        }
    }

    private record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("body")]     string? Body);
}
