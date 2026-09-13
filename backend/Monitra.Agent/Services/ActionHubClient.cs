using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitra.Agent.Models;

namespace Monitra.Agent.Services;

/// <summary>
/// Maintains the persistent connection to AgentHub for real-time EmployeeAction pushes.
/// Auth is the same X-Device-Token header used everywhere else - the .NET SignalR client (unlike
/// a browser) can set arbitrary headers on the WebSocket handshake, so this rides the same
/// TenantIsolationMiddleware device-auth path as the REST endpoints (see Hubs/AgentHub.cs on
/// the API side).
///
/// Initial-connect failures and drops are both handled here: WithAutomaticReconnect() covers a
/// connection that was established and then drops, but a completely failed first connect
/// attempt needs its own retry - EnsureStartedAsync is called every tick from
/// AgentBackgroundService and is a no-op once connected.
/// </summary>
public class ActionHubClient : IAsyncDisposable
{
    private readonly AgentOptions _options;
    private readonly ILogger<ActionHubClient> _logger;
    private HubConnection? _connection;

    public event Func<EmployeeActionPayload, Task>? ActionReceived;

    public ActionHubClient(IOptions<AgentOptions> options, ILogger<ActionHubClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task EnsureStartedAsync(string deviceToken, CancellationToken cancellationToken)
    {
        if (_connection == null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{_options.ApiBaseUrl.TrimEnd('/')}/hubs/agent", options =>
                {
                    options.Headers.Add("X-Device-Token", deviceToken);
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<EmployeeActionPayload>("ReceiveAction", async payload =>
            {
                if (ActionReceived != null)
                {
                    await ActionReceived.Invoke(payload);
                }
            });

            _connection.Closed += ex =>
            {
                _logger.LogWarning(ex, "AgentHub connection closed.");
                return Task.CompletedTask;
            };
        }

        if (_connection.State != HubConnectionState.Disconnected)
        {
            return; // already connected or in the middle of connecting/reconnecting
        }

        try
        {
            await _connection.StartAsync(cancellationToken);
            _logger.LogInformation("Connected to AgentHub for real-time notifications.");
        }
        catch (Exception ex)
        {
            // Expected when the API is briefly unreachable - just retried on the next tick.
            _logger.LogWarning(ex, "Could not connect to AgentHub, will retry.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}
