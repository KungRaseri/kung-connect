using System.Security.Claims;
using KungConnect.Server.Data;
using KungConnect.Server.Data.Entities;
using KungConnect.Shared.Constants;
using KungConnect.Shared.DTOs.Machines;
using KungConnect.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KungConnect.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MachinesController(AppDbContext db, ILogger<MachinesController> logger) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MachineDto>>> GetAll()
    {
        var machines = await db.Machines
            .Where(m => User.IsInRole(Roles.Admin)
                     || m.OwnerId == CurrentUserId
                     || m.OwnerId == null)   // unclaimed self-registered agents visible to all
            .ToListAsync();

        return Ok(machines.Select(m => new MachineDto(
            m.Id, m.Alias, m.Hostname, m.OsType, m.Status, m.AgentVersion, m.LastSeen)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MachineDto>> GetById(Guid id)
    {
        var machine = await db.Machines.FindAsync(id);
        if (machine is null) return NotFound();
        if (!CanAccess(machine)) return Forbid();

        return Ok(new MachineDto(
            machine.Id, machine.Alias, machine.Hostname, machine.OsType,
            machine.Status, machine.AgentVersion, machine.LastSeen));
    }

    /// <summary>Returns full machine details including the agent secret and ready-to-paste config snippet.</summary>
    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<MachineDetailDto>> GetDetail(Guid id)
    {
        var machine = await db.Machines.FindAsync(id);
        if (machine is null) return NotFound();
        if (!CanAccess(machine)) return Forbid();

        var serverUrl = $"{Request.Scheme}://{Request.Host}";
        var snippet = "{\n" +
                      "  \"Agent\": {\n" +
                      $"    \"ServerUrl\": \"{serverUrl}\",\n" +
                      $"    \"MachineAlias\": \"{machine.Alias}\",\n" +
                      $"    \"MachineSecret\": \"{machine.MachineSecret}\",\n" +
                      "    \"AutoAcceptSessions\": true\n" +
                      "  }\n" +
                      "}";

        return Ok(new MachineDetailDto(
            machine.Id, machine.Alias, machine.Hostname,
            machine.OsType, machine.Status, machine.AgentVersion,
            machine.LastSeen, machine.RegisteredAt,
            machine.MachineSecret, machine.AutoAcceptSessions,
            machine.OwnerId is not null, snippet));
    }

    /// <summary>Rename a machine.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateAlias(Guid id, [FromBody] UpdateMachineRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Alias))
            return BadRequest(new { error = "Alias is required." });

        var machine = await db.Machines.FindAsync(id);
        if (machine is null) return NotFound();
        if (!CanAccess(machine)) return Forbid();

        machine.Alias = request.Alias.Trim();
        await db.SaveChangesAsync();
        return Ok(new MachineDto(machine.Id, machine.Alias, machine.Hostname,
            machine.OsType, machine.Status, machine.AgentVersion, machine.LastSeen));
    }

    /// <summary>
    /// Called by an agent on first run to self-register.
    /// Requires a valid user JWT so the machine is tied to a user account.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<MachineDto>> Register([FromBody] RegisterMachineRequest request)
    {
        if (await db.Machines.AnyAsync(m => m.MachineSecret == request.MachineSecret))
            return Conflict(new { message = "Machine already registered" });

        var machine = new MachineEntity
        {
            OwnerId = CurrentUserId,
            Alias = request.Alias,
            Hostname = request.Hostname,
            OsType = request.OsType,
            AgentVersion = request.AgentVersion,
            MachineSecret = request.MachineSecret
        };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        logger.LogInformation("Machine {Alias} registered for user {UserId}", machine.Alias, CurrentUserId);

        return CreatedAtAction(nameof(GetById), new { id = machine.Id },
            new MachineDto(machine.Id, machine.Alias, machine.Hostname,
                machine.OsType, machine.Status, machine.AgentVersion, machine.LastSeen));
    }

    /// <summary>
    /// Creates a machine record and returns the secret to be placed in the agent's appsettings.json.
    /// The machine appears as Online once the agent starts and calls AgentRegister with that secret.
    /// </summary>
    [HttpPost("provision")]
    public async Task<ActionResult<ProvisionMachineResponse>> Provision([FromBody] ProvisionMachineRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Alias))
            return BadRequest(new { error = "Alias is required." });

        var secret = Guid.NewGuid().ToString("N"); // 32-char hex, no hyphens

        var machine = new MachineEntity
        {
            OwnerId       = CurrentUserId,
            Alias         = request.Alias.Trim(),
            Hostname      = string.Empty,
            AgentVersion  = string.Empty,
            MachineSecret = secret
        };

        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        var serverUrl = $"{Request.Scheme}://{Request.Host}";
        var alias     = request.Alias.Trim();
        var snippet   = "{\n" +
                        "  \"Agent\": {\n" +
                        $"    \"ServerUrl\": \"{serverUrl}\",\n" +
                        $"    \"MachineAlias\": \"{alias}\",\n" +
                        $"    \"MachineSecret\": \"{secret}\",\n" +
                        "    \"AutoAcceptSessions\": true\n" +
                        "  }\n" +
                        "}";

        logger.LogInformation("Machine '{Alias}' provisioned for user {UserId}", alias, CurrentUserId);
        return Ok(new ProvisionMachineResponse(machine.Id, secret, snippet));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var machine = await db.Machines.FindAsync(id);
        if (machine is null) return NotFound();
        if (!CanAccess(machine)) return Forbid();

        db.Machines.Remove(machine);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Claim an unowned self-registered machine and optionally rename it.</summary>
    [HttpPost("{id:guid}/claim")]
    public async Task<IActionResult> Claim(Guid id, [FromBody] ClaimMachineRequest request)
    {
        var machine = await db.Machines.FindAsync(id);
        if (machine is null) return NotFound();
        if (!User.IsInRole(Roles.Admin) && machine.OwnerId is not null && machine.OwnerId != CurrentUserId)
            return Forbid();

        machine.OwnerId = CurrentUserId;
        if (!string.IsNullOrWhiteSpace(request.Alias))
            machine.Alias = request.Alias.Trim();
        await db.SaveChangesAsync();

        return Ok(new MachineDto(machine.Id, machine.Alias, machine.Hostname,
            machine.OsType, machine.Status, machine.AgentVersion, machine.LastSeen));
    }

    /// <summary>
    /// Called by the agent's --notify-uninstall mode (WiX CA) just before files are removed.
    /// Marks the machine as Uninstalled so the dashboard can distinguish it from a simple restart.
    /// No user JWT required — the machine secret is the credential.
    /// </summary>
    [HttpPost("notify-uninstall")]
    [AllowAnonymous]
    public async Task<IActionResult> NotifyUninstall([FromBody] NotifyUninstallRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MachineSecret))
            return BadRequest();

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.MachineSecret == request.MachineSecret);
        if (machine is null) return NotFound();

        machine.Status = MachineStatus.Uninstalled;
        machine.SignalRConnectionId = null;
        await db.SaveChangesAsync();

        logger.LogInformation("Machine {Id} ({Alias}) reported uninstall", machine.Id, machine.Alias);
        return NoContent();
    }

    private bool CanAccess(MachineEntity machine) =>
        User.IsInRole(Roles.Admin) || machine.OwnerId == CurrentUserId || machine.OwnerId == null;
}
