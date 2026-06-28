namespace Monitra.Core.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; } // Nullable for Platform level audits
    public string ActorType { get; set; } = string.Empty; // PlatformAdmin, TenantUser, DeviceAgent
    public string ActorId { get; set; } = string.Empty; // User or Device ID
    public string Action { get; set; } = string.Empty; // tenant_created, settings_updated, device_revoked
    public string EntityType { get; set; } = string.Empty; // Tenant, Employee, Device
    public string EntityId { get; set; } = string.Empty; // Target entity identifier
    public string MetadataJson { get; set; } = "{}"; // JSON context data
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
