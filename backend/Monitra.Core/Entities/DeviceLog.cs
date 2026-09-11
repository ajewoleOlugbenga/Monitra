using Monitra.Core.Enums;
using Monitra.Core.Interfaces;

namespace Monitra.Core.Entities;

/// <summary>
/// A structured log line shipped up by the agent (service restarts, connectivity failures,
/// unhandled errors) for IT troubleshooting. Not a behavioral/activity record.
/// </summary>
public class DeviceLog : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }

    public DeviceLogLevel Level { get; set; } = DeviceLogLevel.Info;
    public string Source { get; set; } = "Agent";
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
