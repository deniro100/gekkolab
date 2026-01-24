namespace GekkoLab.Models;

/// <summary>
/// Represents aggregated system metrics stored in the database (1-minute averages)
/// </summary>
public class SystemMetrics
{
    public int Id { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public long DiskUsedBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public DateTime Timestamp { get; set; }
}
