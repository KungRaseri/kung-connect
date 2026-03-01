using System.Security.Claims;
using KungConnect.Server.Data;
using KungConnect.Server.Data.Entities;
using KungConnect.Server.Services;
using KungConnect.Shared.DTOs.Sessions;
using KungConnect.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KungConnect.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController(
    AppDbContext db,
    IMachineRegistry machineRegistry,
    ILogger<SessionsController> logger) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SessionDto>>> GetHistory()
    {
        var sessions = await db.Sessions
            .Include(s => s.Machine)
            .Where(s => s.RequestedByUserId == CurrentUserId)
            .OrderByDescending(s => s.StartedAt)
            .Take(50)
            .ToListAsync();

        return Ok(sessions.Select(s => new SessionDto(
            s.Id, s.MachineId, s.Machine.Alias, s.State,
            s.ConnectionType, s.IsViewOnly, s.StartedAt)));
    }

    /// <summary>
    /// Creates a pending session record. The actual WebRTC handshake is
    /// initiated via the SignalingHub.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SessionDto>> RequestSession([FromBody] RequestSessionDto request)
    {
        var machine = await db.Machines.FindAsync(request.MachineId);
        if (machine is null) return NotFound(new { message = "Machine not found" });

        var agentConnId = await machineRegistry.GetConnectionIdAsync(machine.Id);
        if (agentConnId is null)
            return Conflict(new { message = "Machine is offline" });

        var session = new SessionEntity
        {
            MachineId = request.MachineId,
            RequestedByUserId = CurrentUserId,
            ConnectionType = ConnectionType.Agent,
            State = SessionState.Pending,
            IsViewOnly = request.ViewOnly
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        logger.LogInformation("Session {Id} requested for machine {MachineId}", session.Id, machine.Id);

        return CreatedAtAction(nameof(GetById), new { id = session.Id },
            new SessionDto(session.Id, machine.Id, machine.Alias,
                session.State, session.ConnectionType, session.IsViewOnly, session.StartedAt));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionDto>> GetById(Guid id)
    {
        var session = await db.Sessions.Include(s => s.Machine).FirstOrDefaultAsync(s => s.Id == id);
        if (session is null) return NotFound();
        return Ok(new SessionDto(session.Id, session.MachineId, session.Machine.Alias,
            session.State, session.ConnectionType, session.IsViewOnly, session.StartedAt));
    }
}
