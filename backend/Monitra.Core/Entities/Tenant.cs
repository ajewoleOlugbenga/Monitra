using Monitra.Core.Enums;

namespace Monitra.Core.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public string Timezone { get; set; } = "UTC";
    public TimeSpan DefaultWorkStartTime { get; set; } = TimeSpan.FromHours(9); // 09:00:00
    public TimeSpan DefaultWorkEndTime { get; set; } = TimeSpan.FromHours(17);  // 17:00:00
    public string WorkDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";
    public bool TrackWeekends { get; set; } = false;
    // Also drives the inactivity-justification prompt threshold: an employee idle past this
    // many minutes during the tracking window is asked for a reason (see InactivityIncident).
    public int IdleThresholdMinutes { get; set; } = 5;
    public string UrlTrackingMode { get; set; } = "DomainOnly"; // DomainOnly or FullUrl
    public string KeystrokeTrackingMode { get; set; } = "CountsOnly";
    public int DataRetentionDays { get; set; } = 90;

    // Break policy - enforced server-side (source of truth) and cached on the agent so break
    // requests can be evaluated offline.
    public int BreaksPerDay { get; set; } = 2;
    public int BreakDurationMinutes { get; set; } = 15;

    // After this many unjustified inactivity incidents in a single day, the system should
    // surface the pattern to HR rather than waiting for them to notice it (escalation logic
    // itself is part of the EmployeeAction/notification layer, not yet built).
    public int MaxUnjustifiedInactivityBeforeEscalation { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
