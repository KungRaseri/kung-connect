using Microsoft.AspNetCore.Mvc;

namespace KungConnect.Server.Controllers;

/// <summary>
/// Serves download redirects for the KungConnect desktop client.
/// Configure the URLs via Downloads__WindowsUrl / MacOsUrl / LinuxUrl
/// environment variables (or appsettings.json).
/// </summary>
[ApiController]
[Route("downloads")]
public class DownloadsController(IConfiguration config, ILogger<DownloadsController> logger) : ControllerBase
{
    [HttpGet("windows")]
    public IActionResult Windows() => RedirectToDownload("WindowsUrl");

    [HttpGet("macos")]
    public IActionResult MacOs() => RedirectToDownload("MacOsUrl");

    [HttpGet("linux")]
    public IActionResult Linux() => RedirectToDownload("LinuxUrl");

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
