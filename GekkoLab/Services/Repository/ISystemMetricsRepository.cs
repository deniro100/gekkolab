using GekkoLab.Models;

namespace GekkoLab.Services.Repository;

/// <summary>
/// Repository interface for storing and retrieving aggregated system metrics from the database
/// </summary>
public interface ISystemMetricsRepository
{
    Task SaveMetricsAsync(SystemMetrics metrics);
    Task<SystemMetrics?> GetLatestAsync();
    Task<IEnumerable<SystemMetrics>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<int> GetTotalCountAsync();
    Task CleanupOldMetricsAsync(int maxAgeDays);
}
