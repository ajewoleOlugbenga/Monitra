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
    public int IdleThresholdMinutes { get; set; } = 5;
    public string UrlTrackingMode { get; set; } = "DomainOnly"; // DomainOnly or FullUrl
    public string KeystrokeTrackingMode { get; set; } = "CountsOnly";
    public int DataRetentionDays { get; set; } = 90;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
