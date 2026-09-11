using System.Text.Json;
using Monitra.Agent.Models;

namespace Monitra.Agent.Services;

/// <summary>
/// Persists the last-known tenant policy locally so break-quota and idle-threshold checks keep
/// working when the API is briefly unreachable - see Monitra Architecture Reference §09
/// (offline tolerance). The API remains the source of truth: RequestBreakAsync's server-side
/// check is what actually decides approval, this cache only lets the agent respond instantly
/// and detect thresholds without a round trip on every idle tick.
/// </summary>
public class PolicyCache
{
    private readonly string _filePath;
    private AgentPolicyResponse? _current;

    public PolicyCache()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Monitra");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "policy.json");
    }

    public AgentPolicyResponse Current => _current ?? Defaults();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            _current = JsonSerializer.Deserialize<AgentPolicyResponse>(json);
        }
        catch
        {
            // Corrupt or unreadable cache - fall back to defaults rather than crash startup.
            _current = null;
        }
    }

    public async Task UpdateAsync(AgentPolicyResponse policy, CancellationToken cancellationToken)
    {
        _current = policy;
        var json = JsonSerializer.Serialize(policy);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private static AgentPolicyResponse Defaults() => new()
    {
        IdleThresholdMinutes = 5,
        BreaksPerDay = 2,
        BreakDurationMinutes = 15,
        DefaultWorkStartTime = TimeSpan.FromHours(9),
        DefaultWorkEndTime = TimeSpan.FromHours(17),
        WorkDays = "Monday,Tuesday,Wednesday,Thursday,Friday",
        TrackWeekends = false
    };
}
