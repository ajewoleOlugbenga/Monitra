using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Monitra.Agent.Models;

namespace Monitra.Agent.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient, IOptions<AgentOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.ApiBaseUrl);
    }

    public async Task<AgentRegisterResponse> RegisterAsync(string installToken, CancellationToken cancellationToken)
    {
        var request = new AgentRegisterRequest
        {
            InstallToken = installToken,
            DeviceName = Environment.MachineName,
            OperatingSystem = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}",
            AgentVersion = typeof(ApiClient).Assembly.GetName().Version?.ToString() ?? "0.0.0"
        };

        var response = await _httpClient.PostAsJsonAsync("/api/agent/register", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AgentRegisterResponse>(cancellationToken: cancellationToken);
        return body ?? throw new InvalidOperationException("Registration response body was empty.");
    }

    public async Task<AgentHeartbeatResponse> SendHeartbeatAsync(
        string deviceToken, AgentHeartbeatRequest payload, CancellationToken cancellationToken)
        => await PostDeviceAsync<AgentHeartbeatRequest, AgentHeartbeatResponse>("/api/agent/heartbeat", deviceToken, payload, cancellationToken);

    public async Task<AgentPolicyResponse> GetPolicyAsync(string deviceToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/agent/policy");
        request.Headers.Add("X-Device-Token", deviceToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AgentPolicyResponse>(cancellationToken: cancellationToken);
        return body ?? throw new InvalidOperationException("Policy response body was empty.");
    }

    public async Task<AgentInactivityResponse> ReportInactivityAsync(
        string deviceToken, AgentInactivityRequest payload, CancellationToken cancellationToken)
        => await PostDeviceAsync<AgentInactivityRequest, AgentInactivityResponse>("/api/agent/inactivity", deviceToken, payload, cancellationToken);

    public async Task<AgentBreakResponse> RequestBreakAsync(string deviceToken, CancellationToken cancellationToken)
        => await PostDeviceAsync<object, AgentBreakResponse>("/api/agent/breaks", deviceToken, new { }, cancellationToken);

    public async Task<AgentHealthResponse> ReportHealthAsync(
        string deviceToken, AgentHealthRequest payload, CancellationToken cancellationToken)
        => await PostDeviceAsync<AgentHealthRequest, AgentHealthResponse>("/api/agent/health", deviceToken, payload, cancellationToken);

    public async Task SubmitLogsAsync(string deviceToken, List<AgentLogEntry> entries, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent/logs")
        {
            Content = JsonContent.Create(entries)
        };
        request.Headers.Add("X-Device-Token", deviceToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<TResponse> PostDeviceAsync<TRequest, TResponse>(
        string path, string deviceToken, TRequest payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Device-Token", deviceToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
        return body ?? throw new InvalidOperationException($"{path} response body was empty.");
    }
}
