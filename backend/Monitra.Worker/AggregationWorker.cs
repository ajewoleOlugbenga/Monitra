namespace Monitra.Worker;

/// <summary>
/// Placeholder only - this is Phase 3 of the Monitra Architecture Reference roadmap
/// (aggregation: raw activity/inactivity/break events into daily attendance and productivity
/// summaries) and hasn't been designed yet. This exists so the project has a real entry point
/// and builds, not because there's aggregation logic here.
/// </summary>
public class AggregationWorker : BackgroundService
{
    private readonly ILogger<AggregationWorker> _logger;

    public AggregationWorker(ILogger<AggregationWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Monitra.Worker is a placeholder - no aggregation jobs are implemented yet.");
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
