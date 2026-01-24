using Microsoft.AspNetCore.Mvc;
using GekkoLab.Services.PerformanceMonitoring;
using GekkoLab.Services.Repository;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsStore _metricsStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsStore metricsStore,
        IServiceScopeFactory scopeFactory,
        ILogger<MetricsController> logger)
    {
        _metricsStore = metricsStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get the latest real-time metrics snapshot (from memory)
    /// </summary>
    [HttpGet("realtime/latest")]
    public IActionResult GetLatestRealtime()
    {
        var snapshot = _metricsStore.GetLatest();
        if (snapshot == null)
            return NotFound("No metrics available yet");

        return Ok(new
        {
            snapshot.CpuUsagePercent,
            snapshot.MemoryUsagePercent,
            snapshot.DiskUsagePercent,
            snapshot.MemoryUsedBytes,
            snapshot.MemoryTotalBytes,
            snapshot.DiskUsedBytes,
            snapshot.DiskTotalBytes,
            snapshot.Timestamp
        });
    }

    /// <summary>
    /// Get real-time metrics history from memory (last 2 hours max)
    /// </summary>
    [HttpGet("realtime/history")]
    public IActionResult GetRealtimeHistory([FromQuery] int minutes = 10)
    {
        var duration = TimeSpan.FromMinutes(Math.Min(minutes, 120)); // Max 2 hours
        var snapshots = _metricsStore.GetSnapshots(duration);

        return Ok(snapshots.Select(s => new
        {
            s.CpuUsagePercent,
            s.MemoryUsagePercent,
            s.DiskUsagePercent,
            s.Timestamp
        }));
    }

    /// <summary>
    /// Get aggregated metrics from database (1-minute averages)
    /// </summary>
    [HttpGet("aggregated/history")]
    public async Task<IActionResult> GetAggregatedHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddHours(-2);
        var toDate = to ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemMetricsRepository>();
        
        var metrics = await repository.GetByDateRangeAsync(fromDate, toDate);

        return Ok(metrics.Select(m => new
        {
            m.CpuUsagePercent,
            m.MemoryUsagePercent,
            m.DiskUsagePercent,
            m.MemoryUsedBytes,
            m.MemoryTotalBytes,
            m.DiskUsedBytes,
            m.DiskTotalBytes,
            m.Timestamp
        }));
    }

    /// <summary>
    /// Get the latest aggregated metrics from database
    /// </summary>
    [HttpGet("aggregated/latest")]
    public async Task<IActionResult> GetLatestAggregated()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemMetricsRepository>();
        
        var metrics = await repository.GetLatestAsync();
        if (metrics == null)
            return NotFound("No aggregated metrics available yet");

        return Ok(new
        {
            metrics.CpuUsagePercent,
            metrics.MemoryUsagePercent,
            metrics.DiskUsagePercent,
            metrics.MemoryUsedBytes,
            metrics.MemoryTotalBytes,
            metrics.DiskUsedBytes,
            metrics.DiskTotalBytes,
            metrics.Timestamp
        });
    }

    /// <summary>
    /// Get metrics statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] int hours = 24)
    {
        var from = DateTime.UtcNow.AddHours(-hours);
        var to = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemMetricsRepository>();
        
        var metrics = await repository.GetByDateRangeAsync(from, to);
        var metricsList = metrics.ToList();

        if (!metricsList.Any())
        {
            return Ok(new
            {
                Period = new { From = from, To = to },
                TotalRecords = 0,
                Message = "No metrics available for this period"
            });
        }

        return Ok(new
        {
            Period = new { From = from, To = to },
            TotalRecords = metricsList.Count,
            Cpu = new
            {
                Average = metricsList.Average(m => m.CpuUsagePercent),
                Min = metricsList.Min(m => m.CpuUsagePercent),
                Max = metricsList.Max(m => m.CpuUsagePercent)
            },
            Memory = new
            {
                Average = metricsList.Average(m => m.MemoryUsagePercent),
                Min = metricsList.Min(m => m.MemoryUsagePercent),
                Max = metricsList.Max(m => m.MemoryUsagePercent)
            },
            Disk = new
            {
                Average = metricsList.Average(m => m.DiskUsagePercent),
                Min = metricsList.Min(m => m.DiskUsagePercent),
                Max = metricsList.Max(m => m.DiskUsagePercent)
            }
        });
    }
}
