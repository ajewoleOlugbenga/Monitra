using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public EmployeeMonitoringController(MonitraDbContext dbContext)
    {
        _dbContext = dbContext;
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
}

public class ReviewInactivityRequest
{
    public string Status { get; set; } = string.Empty; // Justified | Unjustified | Escalated
    public string? HrNote { get; set; }
}
