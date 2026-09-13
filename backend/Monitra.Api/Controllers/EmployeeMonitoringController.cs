using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Api.Services;
using Monitra.Core.Entities;
using Monitra.Core.Enums;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Controllers;

// HR/Admin-facing views of behavioral data (inactivity incidents, breaks) and the review action
// on top of it. Deliberately separate from DevicesController - see BehavioralViewPolicy vs
// ITPolicy in Program.cs, and Monitra Architecture Reference §14 for why IT never touches this.
[ApiController]
[Route("api/employees")]
public class EmployeeMonitoringController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;
    private readonly EmployeeActionPusher _actionPusher;

    public EmployeeMonitoringController(MonitraDbContext dbContext, EmployeeActionPusher actionPusher)
    {
        _dbContext = dbContext;
        _actionPusher = actionPusher;
    }

    [HttpGet("{employeeId}/inactivity-incidents")]
    [Authorize(Policy = "BehavioralViewPolicy")]
    public async Task<IActionResult> GetInactivityIncidents(Guid employeeId, [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        // Global tenant query filter already restricts this to the caller's tenant.
        var query = _dbContext.InactivityIncidents
            .Where(i => i.EmployeeId == employeeId)
            .OrderByDescending(i => i.DetectedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

        return Ok(new { total, page, limit, items });
    }

    [HttpPut("inactivity-incidents/{incidentId}/review")]
    [Authorize(Policy = "TenantManagerPolicy")]
    public async Task<IActionResult> ReviewInactivityIncident(Guid incidentId, [FromBody] ReviewInactivityRequest request)
    {
        var incident = await _dbContext.InactivityIncidents.FirstOrDefaultAsync(i => i.Id == incidentId);
        if (incident == null)
        {
            return NotFound("Inactivity incident not found.");
        }

        if (!Enum.TryParse<InactivityIncidentStatus>(request.Status, true, out var status)
            || status == InactivityIncidentStatus.PendingResponse)
        {
            return BadRequest("Status must be Justified, Unjustified, or Escalated.");
        }

        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        incident.Status = status;
        incident.HrNote = request.HrNote;
        incident.ReviewedAt = DateTime.UtcNow;
        incident.ReviewedByTenantUserId = Guid.TryParse(userIdClaim, out var reviewerId) ? reviewerId : null;
        incident.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(new { incident.Id, incident.Status, incident.HrNote });
    }

    [HttpGet("{employeeId}/breaks")]
    [Authorize(Policy = "BehavioralViewPolicy")]
    public async Task<IActionResult> GetBreaks(Guid employeeId, [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        var query = _dbContext.BreakRequests
            .Where(b => b.EmployeeId == employeeId)
            .OrderByDescending(b => b.RequestedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

        return Ok(new { total, page, limit, items });
    }

    [HttpPost("{employeeId}/actions")]
    [Authorize(Policy = "TenantManagerPolicy")]
    public async Task<IActionResult> CreateAction(Guid employeeId, [FromBody] CreateEmployeeActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Message is required.");
        }

        if (!Enum.TryParse<EmployeeActionType>(request.ActionType, true, out var actionType))
        {
            return BadRequest("ActionType must be one of: Message, Warning, CoachingNote, Escalation.");
        }

        var severity = EmployeeActionSeverity.Info;
        if (!string.IsNullOrWhiteSpace(request.Severity)
            && !Enum.TryParse(request.Severity, true, out severity))
        {
            return BadRequest("Severity must be one of: Info, Warning, Critical.");
        }

        var employeeExists = await _dbContext.Employees.AnyAsync(e => e.Id == employeeId);
        if (!employeeExists)
        {
            return NotFound("Employee not found.");
        }

        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        Guid.TryParse(userIdClaim, out var actorId);

        var action = new EmployeeAction
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            ActorTenantUserId = actorId,
            ActionType = actionType,
            Severity = severity,
            Message = request.Message,
            RelatedInactivityIncidentId = request.RelatedInactivityIncidentId,
            Status = EmployeeActionStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.EmployeeActions.Add(action);
        // Save first so EnforceTenantIsolation populates action.TenantId from the caller's
        // JWT claim before we reference it in the audit log below.
        await _dbContext.SaveChangesAsync();

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = action.TenantId,
            ActorType = "TenantUser",
            ActorId = actorId.ToString(),
            Action = "employee_action_created",
            EntityType = "EmployeeAction",
            EntityId = action.Id.ToString(),
            MetadataJson = $"{{\"ActionType\":\"{action.ActionType}\",\"EmployeeId\":\"{employeeId}\"}}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(audit);
        await _dbContext.SaveChangesAsync();

        // Best-effort real-time push - if the employee's agent isn't connected right now, the
        // agent's periodic GET /api/agent/actions/pending poll picks this up once it (re)connects.
        await _actionPusher.PushAsync(action);

        return Ok(new { action.Id, action.Status });
    }

    [HttpGet("{employeeId}/actions")]
    [Authorize(Policy = "BehavioralViewPolicy")]
    public async Task<IActionResult> GetActions(Guid employeeId, [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        var query = _dbContext.EmployeeActions
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

        return Ok(new { total, page, limit, items });
    }
}

public class CreateEmployeeActionRequest
{
    public string ActionType { get; set; } = "Message"; // Message | Warning | CoachingNote | Escalation
    public string? Severity { get; set; } // Info (default) | Warning | Critical
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedInactivityIncidentId { get; set; }
}

public class ReviewInactivityRequest
{
    public string Status { get; set; } = string.Empty; // Justified | Unjustified | Escalated
    public string? HrNote { get; set; }
}
