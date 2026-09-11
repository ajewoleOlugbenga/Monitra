using Monitra.Core.Enums;
using Monitra.Core.Interfaces;

namespace Monitra.Core.Entities;

/// <summary>
/// An employee-initiated break. Auto-approved against the tenant's daily quota
/// (Tenant.BreaksPerDay / BreakDurationMinutes) rather than requiring HR approval - see
/// Monitra Architecture Reference §14 for why. HR only reviews OverQuota requests.
/// </summary>
public class BreakRequest : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid DeviceId { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public int PlannedDurationMinutes { get; set; }
    public DateTime? EndedAt { get; set; }

    public BreakRequestStatus Status { get; set; } = BreakRequestStatus.AutoApproved;

    // Which break of the day this was (1-based), for quota bookkeeping and display.
    public int SequenceForDay { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
