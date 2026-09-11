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
        string deviceToken,
        AgentHeartbeatRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent/heartbeat")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Device-Token", deviceToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AgentHeartbeatResponse>(cancellationToken: cancellationToken);
        return body ?? throw new InvalidOperationException("Heartbeat response body was empty.");
    }
}
