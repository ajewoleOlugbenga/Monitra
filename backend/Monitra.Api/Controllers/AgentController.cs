using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Core.Enums;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Controllers;

// Endpoints consumed by the Monitra desktop agent (Monitra.Agent), not by the web dashboard.
// Authentication here is NOT JWT/cookie based - it's the install-token / device-token scheme
// already enforced by TenantIsolationMiddleware for everything under /api/agent/ except register.
[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;
    private readonly ILogger<AgentController> _logger;

    public AgentController(MonitraDbContext dbContext, ILogger<AgentController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AgentRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InstallToken) || string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return BadRequest("InstallToken and DeviceName are required.");
        }

        var tokenHash = ComputeSha256Hash(request.InstallToken);

        // IgnoreQueryFilters: no tenant context exists yet at this point in the request.
        var installToken = await _dbContext.AgentInstallTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (installToken == null || installToken.Status != TokenStatus.Active)
        {
            return Unauthorized("Invalid or inactive installation token.");
        }

        if (installToken.ExpiresAt.HasValue && installToken.ExpiresAt.Value < DateTime.UtcNow)
        {
            return Unauthorized("Installation token has expired.");
        }

        if (installToken.UsedCount >= installToken.MaxUses)
        {
            return Unauthorized("Installation token has reached its maximum number of uses.");
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == installToken.TenantId);

        if (tenant == null || tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Deleted)
        {
            return Unauthorized("Tenant is not available for device registration.");
        }

        // Establish tenant context for this request so the tenant-scoped writes below are
        // allowed by MonitraDbContext.EnforceTenantIsolation (same convention the
        // TenantIsolationMiddleware uses for already-registered devices).
        HttpContext.Items["TenantId"] = installToken.TenantId;

        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = installToken.TenantId,
            DeviceName = request.DeviceName,
            OperatingSystem = request.OperatingSystem ?? "Unknown",
            AgentVersion = request.AgentVersion ?? "0.0.0",
            DeviceStatus = DeviceStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Devices.Add(device);

        var rawDeviceToken = $"MDT-{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";
        var deviceTokenHash = ComputeSha256Hash(rawDeviceToken);

        var deviceToken = new DeviceToken
        {
            Id = Guid.NewGuid(),
            TenantId = installToken.TenantId,
            DeviceId = device.Id,
            TokenHash = deviceTokenHash,
            Status = "Active",
            IssuedAt = DateTime.UtcNow
        };
        _dbContext.DeviceTokens.Add(deviceToken);

        installToken.UsedCount += 1;
        installToken.UpdatedAt = DateTime.UtcNow;

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = installToken.TenantId,
            ActorType = "DeviceAgent",
            ActorId = device.Id.ToString(),
            Action = "device_registered",
            EntityType = "Device",
            EntityId = device.Id.ToString(),
            MetadataJson = $"{{\"DeviceName\":\"{device.DeviceName}\",\"OperatingSystem\":\"{device.OperatingSystem}\"}}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Device {DeviceId} registered for tenant {TenantId}", device.Id, installToken.TenantId);

        // The raw device token is returned exactly once. Only its SHA-256 hash is persisted.
        return Ok(new AgentRegisterResponse
        {
            DeviceId = device.Id,
            TenantId = installToken.TenantId,
            RawDeviceToken = rawDeviceToken
        });
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] AgentHeartbeatRequest? request)
    {
        // Device-token validation, tenant/device suspension checks, and LastSeenAt/LastUsedAt
        // bookkeeping already happened in TenantIsolationMiddleware before this action runs.
        var deviceId = HttpContext.Items["DeviceId"] as Guid?;
        var tenantId = HttpContext.Items["TenantId"] as Guid?;

        if (request != null)
        {
            // NOTE: this is intentionally a log line, not a database write. There is no
            // activity/event data model yet (see project roadmap) - heartbeat telemetry is
            // accepted so the agent<->API contract exists, but persistence and reporting on
            // idle time / foreground app usage is a follow-up phase, not implemented here.
            _logger.LogInformation(
                "Heartbeat from device {DeviceId}: idle={IdleSeconds}s, app={ForegroundApp}",
                deviceId, request.IdleSeconds, request.ForegroundApp);
        }

        return Ok(new AgentHeartbeatResponse
        {
            ServerTimeUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            TenantId = tenantId
        });
    }

    [HttpPost("inactivity")]
    public async Task<IActionResult> ReportInactivity([FromBody] AgentInactivityRequest request)
    {
        var (tenantId, deviceId, employeeId) = ResolveAgentIdentity();
        if (tenantId == null || deviceId == null)
        {
            return Unauthorized();
        }
        if (employeeId == null)
        {
            return BadRequest("This device is not yet assigned to an employee - cannot record inactivity.");
        }

        var incident = new InactivityIncident
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            EmployeeId = employeeId.Value,
            DeviceId = deviceId.Value,
            DetectedAt = request.DetectedAt == default ? DateTime.UtcNow : request.DetectedAt,
            IdleMinutes = request.IdleMinutes,
            EmployeeReason = request.Reason,
            RespondedAt = string.IsNullOrWhiteSpace(request.Reason) ? null : DateTime.UtcNow,
            // Any reason submitted at time of prompt is provisionally accepted; HR can
            // reclassify as Unjustified after review (PUT .../review below). No reason at all
            // (the employee dismissed the prompt) is left pending for HR's attention.
            Status = string.IsNullOrWhiteSpace(request.Reason)
                ? InactivityIncidentStatus.PendingResponse
                : InactivityIncidentStatus.Justified
        };

        _dbContext.InactivityIncidents.Add(incident);
        await _dbContext.SaveChangesAsync();

        return Ok(new AgentInactivityResponse { IncidentId = incident.Id, Status = incident.Status.ToString() });
    }

    [HttpPost("breaks")]
    public async Task<IActionResult> RequestBreak([FromBody] AgentBreakRequest request)
    {
        var (tenantId, deviceId, employeeId) = ResolveAgentIdentity();
        if (tenantId == null || deviceId == null)
        {
            return Unauthorized();
        }
        if (employeeId == null)
        {
            return BadRequest("This device is not yet assigned to an employee - cannot request a break.");
        }

        var tenant = await _dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId.Value);
        if (tenant == null)
        {
            return Unauthorized();
        }

        // Quota is evaluated server-side (source of truth) even though the agent also caches
        // the policy locally to work offline - see Monitra Architecture Reference §09.
        var todayUtc = DateTime.UtcNow.Date;
        var takenToday = await _dbContext.BreakRequests
            .Where(b => b.EmployeeId == employeeId.Value && b.RequestedAt >= todayUtc)
            .CountAsync();

        var withinQuota = takenToday < tenant.BreaksPerDay;

        var breakRequest = new BreakRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            EmployeeId = employeeId.Value,
            DeviceId = deviceId.Value,
            RequestedAt = DateTime.UtcNow,
            PlannedDurationMinutes = tenant.BreakDurationMinutes,
            SequenceForDay = takenToday + 1,
            Status = withinQuota ? BreakRequestStatus.AutoApproved : BreakRequestStatus.OverQuota
        };

        _dbContext.BreakRequests.Add(breakRequest);
        await _dbContext.SaveChangesAsync();

        return Ok(new AgentBreakResponse
        {
            BreakRequestId = breakRequest.Id,
            Approved = withinQuota,
            PlannedDurationMinutes = breakRequest.PlannedDurationMinutes,
            RemainingBreaksToday = Math.Max(0, tenant.BreaksPerDay - breakRequest.SequenceForDay)
        });
    }

    [HttpPost("health")]
    public async Task<IActionResult> ReportHealth([FromBody] AgentHealthRequest request)
    {
        var (tenantId, deviceId, _) = ResolveAgentIdentity();
        if (tenantId == null || deviceId == null)
        {
            return Unauthorized();
        }

        var status = DeviceHealthStatus.Healthy;
        if (request.CpuUsagePercent >= 90 || request.MemoryUsagePercent >= 90)
        {
            status = DeviceHealthStatus.Critical;
        }
        else if (request.CpuUsagePercent >= 75 || request.MemoryUsagePercent >= 75 || request.DiskFreeGb < 5)
        {
            status = DeviceHealthStatus.Degraded;
        }

        var snapshot = new DeviceHealthSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            DeviceId = deviceId.Value,
            CapturedAt = DateTime.UtcNow,
            CpuUsagePercent = request.CpuUsagePercent,
            MemoryUsagePercent = request.MemoryUsagePercent,
            DiskUsagePercent = request.DiskUsagePercent,
            BatteryPercent = request.BatteryPercent,
            DiskFreeGb = request.DiskFreeGb,
            TopProcessesJson = request.TopProcessesJson ?? "[]",
            Status = status
        };

        _dbContext.DeviceHealthSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync();

        return Ok(new AgentHealthResponse { SnapshotId = snapshot.Id, Status = status.ToString() });
    }

    [HttpPost("logs")]
    public async Task<IActionResult> SubmitLogs([FromBody] List<AgentLogEntry> entries)
    {
        var (tenantId, deviceId, _) = ResolveAgentIdentity();
        if (tenantId == null || deviceId == null)
        {
            return Unauthorized();
        }
        if (entries == null || entries.Count == 0)
        {
            return BadRequest("At least one log entry is required.");
        }

        foreach (var entry in entries.Take(200)) // cap a single batch, avoid unbounded payloads
        {
            _dbContext.DeviceLogs.Add(new DeviceLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                DeviceId = deviceId.Value,
                Level = Enum.TryParse<DeviceLogLevel>(entry.Level, true, out var level) ? level : DeviceLogLevel.Info,
                Source = string.IsNullOrWhiteSpace(entry.Source) ? "Agent" : entry.Source,
                Message = entry.Message,
                CreatedAt = entry.CreatedAt == default ? DateTime.UtcNow : entry.CreatedAt
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { Accepted = Math.Min(entries.Count, 200) });
    }

    private (Guid? TenantId, Guid? DeviceId, Guid? EmployeeId) ResolveAgentIdentity()
    {
        var tenantId = HttpContext.Items["TenantId"] as Guid?;
        var deviceId = HttpContext.Items["DeviceId"] as Guid?;
        var employeeId = HttpContext.Items["EmployeeId"] as Guid?;
        return (tenantId, deviceId, employeeId);
    }

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawData);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}

public class AgentRegisterRequest
{
    public string InstallToken { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? OperatingSystem { get; set; }
    public string? AgentVersion { get; set; }
}

public class AgentRegisterResponse
{
    public Guid DeviceId { get; set; }
    public Guid TenantId { get; set; }
    public string RawDeviceToken { get; set; } = string.Empty;
}

public class AgentHeartbeatRequest
{
    public int? IdleSeconds { get; set; }
    public string? ForegroundApp { get; set; }
}

public class AgentHeartbeatResponse
{
    public DateTime ServerTimeUtc { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? TenantId { get; set; }
}

public class AgentInactivityRequest
{
    public DateTime DetectedAt { get; set; }
    public int IdleMinutes { get; set; }
    public string? Reason { get; set; }
}

public class AgentInactivityResponse
{
    public Guid IncidentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AgentBreakRequest
{
    // Reserved for a future "requested duration" override; v1 always uses the tenant's
    // configured BreakDurationMinutes so the quota math can't be gamed client-side.
}

public class AgentBreakResponse
{
    public Guid BreakRequestId { get; set; }
    public bool Approved { get; set; }
    public int PlannedDurationMinutes { get; set; }
    public int RemainingBreaksToday { get; set; }
}

public class AgentHealthRequest
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public double? BatteryPercent { get; set; }
    public double DiskFreeGb { get; set; }
    public string? TopProcessesJson { get; set; }
}

public class AgentHealthResponse
{
    public Guid SnapshotId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AgentLogEntry
{
    public string Level { get; set; } = "Info";
    public string? Source { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
