using GekkoLab.Models;
using Microsoft.EntityFrameworkCore;

namespace GekkoLab.Services.Repository;

/// <summary>
/// Repository for storing and retrieving aggregated system metrics from the database
/// </summary>
public class SystemMetricsRepository : ISystemMetricsRepository
{
    private readonly GekkoLabDbContext _context;
    private readonly ILogger<SystemMetricsRepository> _logger;

    public SystemMetricsRepository(GekkoLabDbContext context, ILogger<SystemMetricsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveMetricsAsync(SystemMetrics metrics)
    {
        _context.SystemMetrics.Add(metrics);
        await _context.SaveChangesAsync();
        _logger.LogDebug("Saved system metrics: CPU={Cpu:F1}%, Memory={Mem:F1}%, Disk={Disk:F1}%",
            metrics.CpuUsagePercent, metrics.MemoryUsagePercent, metrics.DiskUsagePercent);
    }

    public async Task<SystemMetrics?> GetLatestAsync()
    {
        return await _context.SystemMetrics
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SystemMetrics>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _context.SystemMetrics
            .AsNoTracking()
            .Where(m => m.Timestamp >= from && m.Timestamp <= to)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.SystemMetrics.CountAsync();
    }

    public async Task CleanupOldMetricsAsync(int maxAgeDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        var oldMetrics = await _context.SystemMetrics
            .Where(m => m.Timestamp < cutoff)
            .ToListAsync();

        if (oldMetrics.Any())
        {
            _context.SystemMetrics.RemoveRange(oldMetrics);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} old system metrics records", oldMetrics.Count);
        }
    }
}
