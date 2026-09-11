using Monitra.Core.Enums;
using Monitra.Core.Interfaces;

namespace Monitra.Core.Entities;

/// <summary>
/// Recorded when an employee's idle time crosses the tenant's IdleThresholdMinutes during the
/// tracking window. The agent prompts locally for a reason; this row is the server-side record
/// of that prompt and whatever answer (if any) came back.
/// </summary>
public class InactivityIncident : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid DeviceId { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public int IdleMinutes { get; set; }

    public string? EmployeeReason { get; set; }
    public DateTime? RespondedAt { get; set; }

    public InactivityIncidentStatus Status { get; set; } = InactivityIncidentStatus.PendingResponse;

    public Guid? ReviewedByTenantUserId { get; set; }
    public string? HrNote { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
