using Microsoft.Extensions.Options;
using Monitra.Agent.Models;
using Monitra.Agent.Services;

namespace Monitra.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ApiClient _apiClient;
    private readonly DeviceCredentialStore _credentialStore;
    private readonly AgentOptions _options;

    private DeviceCredentials? _credentials;

    public Worker(
        ILogger<Worker> logger,
        ApiClient apiClient,
        DeviceCredentialStore credentialStore,
        IOptions<AgentOptions> options)
    {
        _logger = logger;
        _apiClient = apiClient;
        _credentialStore = credentialStore;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _credentials = await EnsureRegisteredAsync(stoppingToken);
        if (_credentials == null)
        {
            _logger.LogCritical(
                "Agent is not registered and no install token is configured. " +
                "Set Agent:InstallToken (or MONITRA_INSTALL_TOKEN) and restart the service.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(_options.HeartbeatIntervalSeconds, 15));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Network hiccups / API downtime shouldn't crash the service - just retry
                // on the next interval. A queue-and-flush strategy for missed heartbeats is
                // a follow-up, not implemented in this scaffold.
                _logger.LogWarning(ex, "Heartbeat failed, will retry in {Interval}.", interval);
            }

            await Task.Delay(interval, stoppingToken);
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

        _logger.LogInformation("Registered as device {DeviceId} for tenant {TenantId}.", credentials.DeviceId, credentials.TenantId);
        return credentials;
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var idle = IdleTimeService.GetIdleTime();
        var foregroundApp = ForegroundAppService.GetForegroundProcessName();

        var payload = new AgentHeartbeatRequest
        {
            IdleSeconds = (int)idle.TotalSeconds,
            ForegroundApp = foregroundApp
        };

        var response = await _apiClient.SendHeartbeatAsync(_credentials!.RawDeviceToken, payload, cancellationToken);

        _logger.LogDebug(
            "Heartbeat OK (server time {ServerTime}). Idle={IdleSeconds}s, App={ForegroundApp}",
            response.ServerTimeUtc, payload.IdleSeconds, payload.ForegroundApp);
    }
}
