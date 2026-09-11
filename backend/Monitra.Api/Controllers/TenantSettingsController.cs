using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Core.Interfaces;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Controllers;

// The tenant's own tracking/break/escalation policy - what Owner/Admin configure for their
// organization (Monitra Architecture Reference §07.6). Scoped to the caller's own tenant via
// ITenantProvider; there is deliberately no route parameter for tenant id here.
[ApiController]
[Route("api/tenant/settings")]
[Authorize(Policy = "TenantManagerPolicy")]
public class TenantSettingsController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public TenantSettingsController(MonitraDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var tenant = await CurrentTenantAsync();
        if (tenant == null) return Unauthorized();

        return Ok(new TenantSettingsResponse
        {
            Timezone = tenant.Timezone,
            DefaultWorkStartTime = tenant.DefaultWorkStartTime,
            DefaultWorkEndTime = tenant.DefaultWorkEndTime,
            WorkDays = tenant.WorkDays,
            TrackWeekends = tenant.TrackWeekends,
            IdleThresholdMinutes = tenant.IdleThresholdMinutes,
            UrlTrackingMode = tenant.UrlTrackingMode,
            KeystrokeTrackingMode = tenant.KeystrokeTrackingMode,
            DataRetentionDays = tenant.DataRetentionDays,
            BreaksPerDay = tenant.BreaksPerDay,
            BreakDurationMinutes = tenant.BreakDurationMinutes,
            MaxUnjustifiedInactivityBeforeEscalation = tenant.MaxUnjustifiedInactivityBeforeEscalation
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] TenantSettingsRequest request)
    {
        var tenant = await CurrentTenantAsync();
        if (tenant == null) return Unauthorized();

        if (request.BreaksPerDay < 0 || request.BreakDurationMinutes < 0 || request.IdleThresholdMinutes < 1)
        {
            return BadRequest("Breaks per day, break duration, and idle threshold must be non-negative (idle threshold at least 1).");
        }

        tenant.DefaultWorkStartTime = request.DefaultWorkStartTime;
        tenant.DefaultWorkEndTime = request.DefaultWorkEndTime;
        tenant.WorkDays = request.WorkDays;
        tenant.TrackWeekends = request.TrackWeekends;
        tenant.IdleThresholdMinutes = request.IdleThresholdMinutes;
        tenant.UrlTrackingMode = request.UrlTrackingMode;
        tenant.KeystrokeTrackingMode = request.KeystrokeTrackingMode;
        tenant.DataRetentionDays = request.DataRetentionDays;
        tenant.BreaksPerDay = request.BreaksPerDay;
        tenant.BreakDurationMinutes = request.BreakDurationMinutes;
        tenant.MaxUnjustifiedInactivityBeforeEscalation = request.MaxUnjustifiedInactivityBeforeEscalation;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = "Settings updated." });
    }

    private async Task<Tenant?> CurrentTenantAsync()
    {
        var tenantId = _tenantProvider.TenantId;
        if (tenantId == null || tenantId == Guid.Empty) return null;

        // Tenant itself isn't tenant-scoped (it IS the tenant), so no query filter applies here.
        return await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value);
    }
}

public class TenantSettingsResponse
{
    public string Timezone { get; set; } = "UTC";
    public TimeSpan DefaultWorkStartTime { get; set; }
    public TimeSpan DefaultWorkEndTime { get; set; }
    public string WorkDays { get; set; } = string.Empty;
    public bool TrackWeekends { get; set; }
    public int IdleThresholdMinutes { get; set; }
    public string UrlTrackingMode { get; set; } = string.Empty;
    public string KeystrokeTrackingMode { get; set; } = string.Empty;
    public int DataRetentionDays { get; set; }
    public int BreaksPerDay { get; set; }
    public int BreakDurationMinutes { get; set; }
    public int MaxUnjustifiedInactivityBeforeEscalation { get; set; }
}

public class TenantSettingsRequest
{
    public TimeSpan DefaultWorkStartTime { get; set; }
    public TimeSpan DefaultWorkEndTime { get; set; }
    public string WorkDays { get; set; } = string.Empty;
    public bool TrackWeekends { get; set; }
    public int IdleThresholdMinutes { get; set; }
    public string UrlTrackingMode { get; set; } = string.Empty;
    public string KeystrokeTrackingMode { get; set; } = string.Empty;
    public int DataRetentionDays { get; set; }
    public int BreaksPerDay { get; set; }
    public int BreakDurationMinutes { get; set; }
    public int MaxUnjustifiedInactivityBeforeEscalation { get; set; }
}
