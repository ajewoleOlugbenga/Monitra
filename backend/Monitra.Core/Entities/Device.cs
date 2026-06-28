using Monitra.Core.Enums;
using Monitra.Core.Interfaces;

namespace Monitra.Core.Entities;

public class Device : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public DeviceStatus DeviceStatus { get; set; } = DeviceStatus.Active;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RegisteredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
