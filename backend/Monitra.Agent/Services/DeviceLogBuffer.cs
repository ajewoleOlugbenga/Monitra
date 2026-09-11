using System.Collections.Concurrent;
using Monitra.Agent.Models;

namespace Monitra.Agent.Services;

/// <summary>
/// In-memory queue of the agent's own operational log lines (registration, connectivity
/// failures, errors), flushed to the API on an interval. Not a replacement for local file
/// logging - if the process crashes before a flush, those entries are lost, which is an
/// acceptable tradeoff for a diagnostics feed rather than an audit trail.
/// </summary>
public class DeviceLogBuffer
{
    private readonly ConcurrentQueue<AgentLogEntry> _entries = new();

    public void Add(string level, string message, string source = "Agent")
    {
        _entries.Enqueue(new AgentLogEntry
        {
            Level = level,
            Source = source,
            Message = message,
            CreatedAt = DateTime.UtcNow
        });
    }

    public List<AgentLogEntry> DrainUpTo(int max)
    {
        var batch = new List<AgentLogEntry>();
        while (batch.Count < max && _entries.TryDequeue(out var entry))
        {
            batch.Add(entry);
        }
        return batch;
    }
}
