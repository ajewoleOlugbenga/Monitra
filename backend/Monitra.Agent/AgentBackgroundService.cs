using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitra.Agent.Models;
using Monitra.Agent.Services;
using Monitra.Agent.Tray;

namespace Monitra.Agent;

/// <summary>
/// Runs on the generic host's thread pool, independent of the WinForms message loop that
/// TrayContext owns. Ticks on a short base interval and does whatever's due: heartbeat, idle
/// checks, health/log/policy uploads. Talks to the tray only through TrayContext's own
/// thread-marshaling methods - never touches WinForms controls directly.
/// </summary>
public class AgentBackgroundService : BackgroundService
{
    private static readonly TimeSpan BaseTick = TimeSpan.FromSeconds(10);
    private const int ActiveThresholdSeconds = 5; // idle below this counts as "active again"

    private readonly ILogger<AgentBackgroundService> _logger;
    private readonly ApiClient _apiClient;
    private readonly DeviceCredentialStore _credentialStore;
    private readonly PolicyCache _policyCache;
    private readonly DeviceHealthService _healthService;
    private readonly DeviceLogBuffer _logBuffer;
    private readonly TrayContext _tray;
    private readonly AgentOptions _options;

    private DeviceCredentials? _credentials;
    private bool _promptedForCurrentIdlePeriod;
    private DateTime? _activeBreakEndsAtUtc;

    private DateTime _nextHeartbeatDue = DateTime.MinValue;
    private DateTime _nextHealthDue = DateTime.MinValue;
    private DateTime _nextPolicyRefreshDue = DateTime.MinValue;
    private DateTime _nextLogFlushDue = DateTime.MinValue;

    public AgentBackgroundService(
        ILogger<AgentBackgroundService> logger,
        ApiClient apiClient,
        DeviceCredentialStore credentialStore,
        PolicyCache policyCache,
        DeviceHealthService healthService,
        DeviceLogBuffer logBuffer,
        TrayContext tray,
        IOptions<AgentOptions> options)
    {
        _logger = logger;
        _apiClient = apiClient;
        _credentialStore = credentialStore;
        _policyCache = policyCache;
        _healthService = healthService;
        _logBuffer = logBuffer;
        _tray = tray;
        _options = options.Value;

        _tray.BreakRequested += OnBreakRequestedAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _policyCache.LoadAsync(stoppingToken);

        _credentials = await EnsureRegisteredAsync(stoppingToken);
        if (_credentials == null)
        {
            _tray.UpdateStatus("Not registered - set an install token");
            _logger.LogCritical(
                "Agent is not registered and no install token is configured. " +
                "Set Agent:InstallToken (or MONITRA_INSTALL_TOKEN) and restart.");
            return;
        }

        await RefreshPolicyAsync(stoppingToken);
        _tray.UpdateStatus("Monitra - active");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logBuffer.Add("Error", $"Tick failed: {ex.Message}");
                _logger.LogWarning(ex, "Agent tick failed, continuing.");
            }

            await Task.Delay(BaseTick, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var idle = IdleTimeService.GetIdleTime();
        var foregroundApp = ForegroundAppService.GetForegroundProcessName();
        var now = DateTime.UtcNow;

        var onApprovedBreak = _activeBreakEndsAtUtc.HasValue && now < _activeBreakEndsAtUtc.Value;
        if (_activeBreakEndsAtUtc.HasValue && now >= _activeBreakEndsAtUtc.Value)
        {
            _activeBreakEndsAtUtc = null;
        }

        if (idle.TotalSeconds <= ActiveThresholdSeconds)
        {
            _promptedForCurrentIdlePeriod = false;
        }
        else if (!onApprovedBreak
                 && !_promptedForCurrentIdlePeriod
                 && idle.TotalMinutes >= _policyCache.Current.IdleThresholdMinutes
                 && WithinTrackingWindow(now))
        {
            _promptedForCurrentIdlePeriod = true;
            await HandleInactivityAsync((int)idle.TotalMinutes, ct);
        }

        _tray.UpdateStatus(onApprovedBreak
            ? $"On break until {_activeBreakEndsAtUtc:HH:mm} UTC"
            : $"Active - idle {(int)idle.TotalSeconds}s - {foregroundApp ?? "unknown app"}");

        if (now >= _nextHeartbeatDue)
        {
            _nextHeartbeatDue = now.AddSeconds(Math.Max(_options.HeartbeatIntervalSeconds, 15));
            await SendHeartbeatAsync((int)idle.TotalSeconds, foregroundApp, ct);
        }

        if (now >= _nextHealthDue)
        {
            _nextHealthDue = now.AddSeconds(Math.Max(_options.HealthReportIntervalSeconds, 60));
            await ReportHealthAsync(ct);
        }

        if (now >= _nextPolicyRefreshDue)
        {
            _nextPolicyRefreshDue = now.AddSeconds(Math.Max(_options.PolicyRefreshIntervalSeconds, 300));
            await RefreshPolicyAsync(ct);
        }

        if (now >= _nextLogFlushDue)
        {
            _nextLogFlushDue = now.AddSeconds(Math.Max(_options.LogFlushIntervalSeconds, 30));
            await FlushLogsAsync(ct);
        }
    }

    private bool WithinTrackingWindow(DateTime nowUtc)
    {
        // Simplification for this pass: compares against the machine's local time directly
        // rather than the tenant's configured Timezone. Good enough for a single-region pilot;
        // tenant-timezone-aware scheduling is a follow-up.
        var policy = _policyCache.Current;
        var local = nowUtc.ToLocalTime();

        if (!policy.TrackWeekends)
        {
            var isWeekend = local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var workDays = policy.WorkDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var todayName = local.DayOfWeek.ToString();
            if (isWeekend && !workDays.Contains(todayName))
            {
                return false;
            }
        }

        var timeOfDay = local.TimeOfDay;
        return timeOfDay >= policy.DefaultWorkStartTime && timeOfDay <= policy.DefaultWorkEndTime;
    }

    private async Task HandleInactivityAsync(int idleMinutes, CancellationToken ct)
    {
        try
        {
            var reason = await _tray.ShowInactivityPromptAsync(idleMinutes);

            var response = await _apiClient.ReportInactivityAsync(_credentials!.RawDeviceToken, new AgentInactivityRequest
            {
                DetectedAt = DateTime.UtcNow,
                IdleMinutes = idleMinutes,
                Reason = reason
            }, ct);

            _logBuffer.Add("Info", $"Inactivity incident {response.IncidentId} recorded ({response.Status}).");
        }
        catch (Exception ex)
        {
            _logBuffer.Add("Warning", $"Failed to report inactivity: {ex.Message}");
            _logger.LogWarning(ex, "Failed to report inactivity incident.");
        }
    }

    private async Task OnBreakRequestedAsync()
    {
        try
        {
            var response = await _apiClient.RequestBreakAsync(_credentials!.RawDeviceToken, CancellationToken.None);
            if (response.Approved)
            {
                _activeBreakEndsAtUtc = DateTime.UtcNow.AddMinutes(response.PlannedDurationMinutes);
                _promptedForCurrentIdlePeriod = true; // suppress inactivity prompts for the break
                _tray.ShowBalloon("Break started",
                    $"Enjoy your {response.PlannedDurationMinutes}-minute break. " +
                    $"{response.RemainingBreaksToday} more available today.");
            }
            else
            {
                _tray.ShowBalloon("Break request logged",
                    "You've used all your breaks for today. This request was logged for your manager to see.",
                    System.Windows.Forms.ToolTipIcon.Warning);
            }
            _logBuffer.Add("Info", $"Break request {response.BreakRequestId}: approved={response.Approved}.");
        }
        catch (Exception ex)
        {
            _tray.ShowBalloon("Break request failed", "Couldn't reach Monitra - try again in a moment.", System.Windows.Forms.ToolTipIcon.Error);
            _logBuffer.Add("Error", $"Break request failed: {ex.Message}");
            _logger.LogWarning(ex, "Break request failed.");
        }
    }

    private async Task SendHeartbeatAsync(int idleSeconds, string? foregroundApp, CancellationToken ct)
    {
        try
        {
            await _apiClient.SendHeartbeatAsync(_credentials!.RawDeviceToken, new AgentHeartbeatRequest
            {
                IdleSeconds = idleSeconds,
                ForegroundApp = foregroundApp
            }, ct);
        }
        catch (Exception ex)
        {
            _logBuffer.Add("Warning", $"Heartbeat failed: {ex.Message}");
            _logger.LogWarning(ex, "Heartbeat failed, will retry next tick.");
        }
    }

    private async Task ReportHealthAsync(CancellationToken ct)
    {
        try
        {
            var reading = await _healthService.SampleAsync(ct);
            await _apiClient.ReportHealthAsync(_credentials!.RawDeviceToken, new AgentHealthRequest
            {
                CpuUsagePercent = reading.CpuUsagePercent,
                MemoryUsagePercent = reading.MemoryUsagePercent,
                DiskUsagePercent = reading.DiskUsagePercent,
                BatteryPercent = reading.BatteryPercent,
                DiskFreeGb = reading.DiskFreeGb,
                TopProcessesJson = reading.TopProcessesJson
            }, ct);
        }
        catch (Exception ex)
        {
            _logBuffer.Add("Warning", $"Health report failed: {ex.Message}");
            _logger.LogWarning(ex, "Health report failed.");
        }
    }

    private async Task RefreshPolicyAsync(CancellationToken ct)
    {
        if (_credentials == null) return;
        try
        {
            var policy = await _apiClient.GetPolicyAsync(_credentials.RawDeviceToken, ct);
            await _policyCache.UpdateAsync(policy, ct);
        }
        catch (Exception ex)
        {
            // Not fatal - PolicyCache.Current falls back to the last-known value (or built-in
            // defaults on first run), which is the whole point of caching it locally.
            _logBuffer.Add("Warning", $"Policy refresh failed, using cached policy: {ex.Message}");
            _logger.LogWarning(ex, "Policy refresh failed, continuing with cached policy.");
        }
    }

    private async Task FlushLogsAsync(CancellationToken ct)
    {
        if (_credentials == null) return;
        var batch = _logBuffer.DrainUpTo(100);
        if (batch.Count == 0) return;

        try
        {
            await _apiClient.SubmitLogsAsync(_credentials.RawDeviceToken, batch, ct);
        }
        catch
        {
            // Best-effort: put the batch back so it's retried next flush rather than lost.
            foreach (var entry in batch)
            {
                _logBuffer.Add(entry.Level, entry.Message, entry.Source ?? "Agent");
            }
        }
    }

    private async Task<DeviceCredentials?> EnsureRegisteredAsync(CancellationToken cancellationToken)
    {
        var existing = await _credentialStore.LoadAsync(cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("Loaded existing device registration {DeviceId}.", existing.DeviceId);
            return existing;
        }

        var installToken = _options.InstallToken;
        if (string.IsNullOrWhiteSpace(installToken))
        {
            installToken = Environment.GetEnvironmentVariable("MONITRA_INSTALL_TOKEN");
        }

        if (string.IsNullOrWhiteSpace(installToken))
        {
            return null;
        }

        _logger.LogInformation("No existing registration found - registering with the API.");
        var response = await _apiClient.RegisterAsync(installToken, cancellationToken);

        var credentials = new DeviceCredentials(response.DeviceId, response.TenantId, response.RawDeviceToken);
        await _credentialStore.SaveAsync(credentials, cancellationToken);

        _logBuffer.Add("Info", $"Registered as device {credentials.DeviceId}.");
        _logger.LogInformation("Registered as device {DeviceId} for tenant {TenantId}.", credentials.DeviceId, credentials.TenantId);
        return credentials;
    }
}
