using KungConnect.Server.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KungConnect.Server.Controllers;

/// <summary>
/// Redirects download requests to GitHub Releases artifacts.
/// Defaults point to github.com/KungRaseri/kung-connect/releases/latest
/// and can be overridden via Downloads__ environment variables.
/// </summary>
[ApiController]
[Route("downloads")]
public class DownloadsController(IOptions<DownloadsOptions> opts) : ControllerBase
{
    private DownloadsOptions O => opts.Value;

    // ── Desktop client (handles kungconnect:// URI scheme, used on the /join page) ─

    [HttpGet("windows")]
    public IActionResult Windows()   => Redirect(O.WindowsUrl);

    [HttpGet("macos")]
    public IActionResult MacOs()     => Redirect(O.MacOsUrl);

    [HttpGet("linux")]
    public IActionResult Linux()     => Redirect(O.LinuxUrl);

    // ── Agent (background service installed on machines to be managed) ──────

    [HttpGet("agent/windows")]
    public IActionResult AgentWindows()    => Redirect(O.AgentWindowsUrl);

    [HttpGet("agent/macos")]
    public IActionResult AgentMacOs()      => Redirect(O.AgentMacOsUrl);

    [HttpGet("agent/linux")]
    public IActionResult AgentLinux()      => Redirect(O.AgentLinuxUrl);

    [HttpGet("agent/linux-arm64")]
    public IActionResult AgentLinuxArm64() => Redirect(O.AgentLinuxArm64Url);
}
