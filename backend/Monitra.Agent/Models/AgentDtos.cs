namespace Monitra.Agent.Models;

// Mirrors the request/response shapes in Monitra.Api's AgentController. Kept as a separate,
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
