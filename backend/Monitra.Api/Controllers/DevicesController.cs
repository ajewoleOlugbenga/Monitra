using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Controllers;

// IT-facing fleet health and logs. Gated by ITPolicy (Owner + ITSupport only) - never exposes
// employee behavioral data, only machine telemetry. See Monitra Architecture Reference §14.
[ApiController]
[Route("api/devices")]
[Authorize(Policy = "ITPolicy")]
public class DevicesController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;

    public DevicesController(MonitraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        // Latest health snapshot per device, joined onto the device list, for the fleet
        // overview (Healthy / Degraded / Critical counts + table).
        var devices = await _dbContext.Devices.AsNoTracking().ToListAsync();

        var latestSnapshots = await _dbContext.DeviceHealthSnapshots
            .AsNoTracking()
            .GroupBy(h => h.DeviceId)
            .Select(g => g.OrderByDescending(h => h.CapturedAt).First())
            .ToListAsync();

        var snapshotByDevice = latestSnapshots.ToDictionary(s => s.DeviceId);

        var result = devices.Select(d => new
        {
            d.Id,
            d.DeviceName,
            d.OperatingSystem,
            d.AgentVersion,
            d.DeviceStatus,
            d.EmployeeId,
            d.LastSeenAt,
            Health = snapshotByDevice.TryGetValue(d.Id, out var snap)
                ? new { snap.Status, snap.CpuUsagePercent, snap.MemoryUsagePercent, snap.DiskFreeGb, snap.CapturedAt }
                : null
        });

        return Ok(result);
    }

    [HttpGet("{deviceId}/health")]
    public async Task<IActionResult> GetHealthHistory(Guid deviceId, [FromQuery] int limit = 100)
    {
        var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device == null)
        {
            return NotFound("Device not found.");
        }

        var snapshots = await _dbContext.DeviceHealthSnapshots
            .Where(h => h.DeviceId == deviceId)
            .OrderByDescending(h => h.CapturedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync();

        return Ok(snapshots);
    }

    [HttpGet("{deviceId}/logs")]
    public async Task<IActionResult> GetLogs(Guid deviceId, [FromQuery] int limit = 200)
    {
        var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device == null)
        {
            return NotFound("Device not found.");
        }

        var logs = await _dbContext.DeviceLogs
            .Where(l => l.DeviceId == deviceId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync();

        return Ok(logs);
    }
}
