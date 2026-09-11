using Monitra.Core.Enums;
using Monitra.Core.Interfaces;

namespace Monitra.Core.Entities;

/// <summary>
/// A periodic resource-usage reading from a device, for the IT fleet-health dashboard.
/// Deliberately carries no behavioral data (no app names beyond resource attribution, no URLs).
/// </summary>
public class DeviceHealthSnapshot : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public double? BatteryPercent { get; set; }
    public double DiskFreeGb { get; set; }

    // JSON array of { ProcessName, CpuPercent, MemoryMb } for the top resource-consuming
    // processes at capture time - lets IT answer "what's slowing this machine down" without a
    // separate table per process sample.
    public string TopProcessesJson { get; set; } = "[]";

    public DeviceHealthStatus Status { get; set; } = DeviceHealthStatus.Healthy;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
