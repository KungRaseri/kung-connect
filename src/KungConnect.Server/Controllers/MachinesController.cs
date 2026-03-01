using System.Security.Claims;
using KungConnect.Server.Data;
using KungConnect.Server.Data.Entities;
using KungConnect.Shared.Constants;
using KungConnect.Shared.DTOs.Machines;
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
            .Where(m => User.IsInRole(Roles.Admin) || m.OwnerId == CurrentUserId)
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

    private bool CanAccess(MachineEntity machine) =>
        User.IsInRole(Roles.Admin) || machine.OwnerId == CurrentUserId;
}
