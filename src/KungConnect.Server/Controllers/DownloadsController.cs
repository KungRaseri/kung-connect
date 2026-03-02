using Microsoft.AspNetCore.Mvc;

namespace KungConnect.Server.Controllers;

/// <summary>
/// Serves download redirects for the KungConnect desktop client and agent.
/// Configure the URLs via environment variables (or appsettings.json).
/// Client: Downloads__WindowsUrl / MacOsUrl / LinuxUrl
/// Agent:  Downloads__AgentWindowsUrl / AgentMacOsUrl / AgentLinuxUrl / AgentLinuxArm64Url
/// </summary>
[ApiController]
[Route("downloads")]
public class DownloadsController(IConfiguration config, ILogger<DownloadsController> logger) : ControllerBase
{
    // ── Desktop client (handles kungconnect:// URI scheme, used on the /join page) ─

    [HttpGet("windows")]
    public IActionResult Windows() => RedirectToDownload("WindowsUrl");

    [HttpGet("macos")]
    public IActionResult MacOs() => RedirectToDownload("MacOsUrl");

    [HttpGet("linux")]
    public IActionResult Linux() => RedirectToDownload("LinuxUrl");

    // ── Agent (background service installed on machines to be managed) ──────

    [HttpGet("agent/windows")]
    public IActionResult AgentWindows() => RedirectToDownload("AgentWindowsUrl");

    [HttpGet("agent/macos")]
    public IActionResult AgentMacOs() => RedirectToDownload("AgentMacOsUrl");

    [HttpGet("agent/linux")]
    public IActionResult AgentLinux() => RedirectToDownload("AgentLinuxUrl");

    [HttpGet("agent/linux-arm64")]
    public IActionResult AgentLinuxArm64() => RedirectToDownload("AgentLinuxArm64Url");

    // ────────────────────────────────────────────────────────────────

    private IActionResult RedirectToDownload(string key)
    {
        var url = config[$"Downloads:{key}"];
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("Download requested for {Key} but Downloads:{Key} is not configured", key, key);
            return NotFound(new { message = $"No download is configured yet. Set the Downloads__{key} environment variable." });
        }
        return Redirect(url);
    }
}
