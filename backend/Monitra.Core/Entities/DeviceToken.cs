using Monitra.Core.Interfaces;

namespace Monitra.Core.Entities;

public class DeviceToken : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public string TokenHash { get; set; } = string.Empty; // SHA-256 hash of the device token
    public string Status { get; set; } = "Active";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
