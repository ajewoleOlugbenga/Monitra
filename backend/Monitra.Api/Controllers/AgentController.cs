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
