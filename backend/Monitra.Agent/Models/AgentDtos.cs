namespace Monitra.Agent.Models;

// Mirrors the request/response shapes in Monitra.Api's controllers. Kept as a separate,
// independently-versioned contract rather than a shared project reference: the agent ships to
// customer machines on its own release cadence and can't assume it's always talking to the
// exact same API build.

public class AgentRegisterRequest
{
    public string InstallToken { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? OperatingSystem { get; set; }
    public string? AgentVersion { get; set; }
}

public class AgentRegisterResponse
{
    public Guid DeviceId { get; set; }
    public Guid TenantId { get; set; }
    public string RawDeviceToken { get; set; } = string.Empty;
}

public class AgentHeartbeatRequest
{
    public int? IdleSeconds { get; set; }
    public string? ForegroundApp { get; set; }
}

public class AgentHeartbeatResponse
{
    public DateTime ServerTimeUtc { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? TenantId { get; set; }
}

public class AgentPolicyResponse
{
    public int IdleThresholdMinutes { get; set; }
    public int BreaksPerDay { get; set; }
    public int BreakDurationMinutes { get; set; }
    public TimeSpan DefaultWorkStartTime { get; set; }
    public TimeSpan DefaultWorkEndTime { get; set; }
    public string WorkDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";
    public bool TrackWeekends { get; set; }
}

public class AgentInactivityRequest
{
    public DateTime DetectedAt { get; set; }
    public int IdleMinutes { get; set; }
    public string? Reason { get; set; }
}

public class AgentInactivityResponse
{
    public Guid IncidentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AgentBreakResponse
{
    public Guid BreakRequestId { get; set; }
    public bool Approved { get; set; }
    public int PlannedDurationMinutes { get; set; }
    public int RemainingBreaksToday { get; set; }
}

public class AgentHealthRequest
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public double? BatteryPercent { get; set; }
    public double DiskFreeGb { get; set; }
    public string? TopProcessesJson { get; set; }
}

public class AgentHealthResponse
{
    public Guid SnapshotId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class EmployeeActionPayload
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AgentLogEntry
{
    public string Level { get; set; } = "Info";
    public string? Source { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
