using GekkoLab.Models;
using GekkoLab.Services.Repository;

namespace GekkoLab.Services.PerformanceMonitoring;

/// <summary>
/// Background service that collects system metrics:
/// - Every 5 seconds: stores in memory for real-time display
/// - Every 1 minute: aggregates and stores in database
/// </summary>
public class PerformanceMonitoringService : BackgroundService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ISystemMetricsCollector _collector;
    private readonly IMetricsStore _metricsStore;
    private readonly IServiceScopeFactory _scopeFactory;

    public PerformanceMonitoringService(
        ILogger<PerformanceMonitoringService> logger,
        IConfiguration configuration,
        ISystemMetricsCollectorProvider collectorProvider,
        IMetricsStore metricsStore,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _collector = collectorProvider.GetCollector();
        _metricsStore = metricsStore;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("PerformanceMonitoring:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("Performance monitoring is disabled via configuration");
            return;
        }

        var snapshotInterval = _configuration.GetValue<TimeSpan>("PerformanceMonitoring:SnapshotInterval", TimeSpan.FromSeconds(5));
        var aggregationInterval = _configuration.GetValue<TimeSpan>("PerformanceMonitoring:AggregationInterval", TimeSpan.FromMinutes(1));

        _logger.LogInformation("Performance monitoring started. Snapshot interval: {Snapshot}, Aggregation interval: {Aggregation}", snapshotInterval, aggregationInterval);

        var lastAggregationTime = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectSnapshotAsync();

            if (DateTime.UtcNow - lastAggregationTime >= aggregationInterval)
            {
                await AggregateAndStoreAsync();
                lastAggregationTime = DateTime.UtcNow;
            }

            try
            {
                await Task.Delay(snapshotInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CollectSnapshotAsync()
    {
        try
        {
            var snapshot = await _collector.CollectMetricsAsync();
            _metricsStore.AddSnapshot(snapshot);

            _logger.LogDebug(
                "Metrics snapshot: CPU={Cpu:F1}%, Memory={Mem:F1}%, Disk={Disk:F1}%",
                snapshot.CpuUsagePercent, snapshot.MemoryUsagePercent, snapshot.DiskUsagePercent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting metrics snapshot");
        }
    }

    private async Task AggregateAndStoreAsync()
    {
        try
        {
            var snapshots = _metricsStore.GetSnapshotsForAggregation();

            if (!snapshots.Any())
            {
                _logger.LogDebug("No snapshots available for aggregation");
                return;
            }

            // Calculate averages
            var aggregatedMetrics = new SystemMetrics
            {
                CpuUsagePercent = snapshots.Average(s => s.CpuUsagePercent),
                MemoryUsagePercent = snapshots.Average(s => s.MemoryUsagePercent),
                DiskUsagePercent = snapshots.Average(s => s.DiskUsagePercent),
                MemoryUsedBytes = (long)snapshots.Average(s => s.MemoryUsedBytes),
                MemoryTotalBytes = snapshots.Last().MemoryTotalBytes,
                DiskUsedBytes = (long)snapshots.Average(s => s.DiskUsedBytes),
                DiskTotalBytes = snapshots.Last().DiskTotalBytes,
                Timestamp = DateTime.UtcNow
            };

            // Store in database
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISystemMetricsRepository>();
            await repository.SaveMetricsAsync(aggregatedMetrics);

            _logger.LogInformation(
                "Aggregated metrics saved: CPU={Cpu:F1}%, Memory={Mem:F1}%, Disk={Disk:F1}% (from {Count} snapshots)",
                aggregatedMetrics.CpuUsagePercent, 
                aggregatedMetrics.MemoryUsagePercent, 
                aggregatedMetrics.DiskUsagePercent,
                snapshots.Count);

            // Periodic cleanup of old database records
            var maxAgeDays = _configuration.GetValue<int>("PerformanceMonitoring:MaxAgeDays", 7);
            if (DateTime.UtcNow.Minute == 0) // Run cleanup once per hour
            {
                await repository.CleanupOldMetricsAsync(maxAgeDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating and storing metrics");
        }
    }
}
