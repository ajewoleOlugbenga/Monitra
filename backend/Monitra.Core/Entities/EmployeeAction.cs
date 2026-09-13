using Monitra.Core.Enums;
using Monitra.Core.Interfaces;

namespace Monitra.Core.Entities;

/// <summary>
/// A warning, coaching note, message, or escalation an HR/Admin user issues against an
/// employee - typically off the back of an InactivityIncident or a productivity report.
/// Delivered in real time to the employee's agent over SignalR when connected (see
/// Hubs/AgentHub); RelatedInactivityIncidentId links it back to what prompted it, when
/// applicable.
/// </summary>
public class EmployeeAction : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ActorTenantUserId { get; set; }

    public EmployeeActionType ActionType { get; set; } = EmployeeActionType.Message;
    public EmployeeActionSeverity Severity { get; set; } = EmployeeActionSeverity.Info;
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedInactivityIncidentId { get; set; }

    public EmployeeActionStatus Status { get; set; } = EmployeeActionStatus.Sent;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
