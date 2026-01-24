using System.Diagnostics;

namespace GekkoLab.Services.PerformanceMonitoring;

/// <summary>
/// Interface for collecting system metrics
/// </summary>
public interface ISystemMetricsCollector
{
    Task<MetricsSnapshot> CollectMetricsAsync();
}

/// <summary>
/// Collects system metrics (CPU, memory, disk) on Linux/Raspberry Pi
/// </summary>
public class LinuxSystemMetricsCollector : ISystemMetricsCollector
{
    private readonly ILogger<LinuxSystemMetricsCollector> _logger;
    private double _previousIdleTime;
    private double _previousTotalTime;
    private bool _isFirstReading = true;

    public LinuxSystemMetricsCollector(ILogger<LinuxSystemMetricsCollector> logger)
    {
        _logger = logger;
    }

    public async Task<MetricsSnapshot> CollectMetricsAsync()
    {
        var snapshot = new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Collect CPU usage
            snapshot.CpuUsagePercent = await GetCpuUsageAsync();

            // Collect memory usage
            var (memUsed, memTotal) = await GetMemoryUsageAsync();
            snapshot.MemoryUsedBytes = memUsed;
            snapshot.MemoryTotalBytes = memTotal;
            snapshot.MemoryUsagePercent = memTotal > 0 ? (double)memUsed / memTotal * 100 : 0;

            // Collect disk usage
            var (diskUsed, diskTotal) = GetDiskUsage();
            snapshot.DiskUsedBytes = diskUsed;
            snapshot.DiskTotalBytes = diskTotal;
            snapshot.DiskUsagePercent = diskTotal > 0 ? (double)diskUsed / diskTotal * 100 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting system metrics");
        }

        return snapshot;
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            // Read /proc/stat for CPU times
            var statContent = await File.ReadAllTextAsync("/proc/stat");
            var cpuLine = statContent.Split('\n').FirstOrDefault(l => l.StartsWith("cpu "));
            
            if (cpuLine == null)
            {
                _logger.LogWarning("Could not find CPU stats in /proc/stat");
                return 0;
            }

            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return 0;
            }

            // CPU times: user, nice, system, idle, iowait, irq, softirq, steal
            var user = double.Parse(parts[1]);
            var nice = double.Parse(parts[2]);
            var system = double.Parse(parts[3]);
            var idle = double.Parse(parts[4]);
            var iowait = parts.Length > 5 ? double.Parse(parts[5]) : 0;

            var idleTime = idle + iowait;
            var totalTime = user + nice + system + idle + iowait;
            
            if (parts.Length > 6) totalTime += double.Parse(parts[6]); // irq
            if (parts.Length > 7) totalTime += double.Parse(parts[7]); // softirq
            if (parts.Length > 8) totalTime += double.Parse(parts[8]); // steal

            if (_isFirstReading)
            {
                _previousIdleTime = idleTime;
                _previousTotalTime = totalTime;
                _isFirstReading = false;
                return 0;
            }

            var idleDelta = idleTime - _previousIdleTime;
            var totalDelta = totalTime - _previousTotalTime;

            _previousIdleTime = idleTime;
            _previousTotalTime = totalTime;

            if (totalDelta == 0)
            {
                return 0;
            }

            var cpuUsage = (1.0 - idleDelta / totalDelta) * 100;
            return Math.Max(0, Math.Min(100, cpuUsage));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error reading CPU usage from /proc/stat");
            return 0;
        }
    }

    private async Task<(long used, long total)> GetMemoryUsageAsync()
    {
        try
        {
            var memInfo = await File.ReadAllTextAsync("/proc/meminfo");
            var lines = memInfo.Split('\n');

            long memTotal = 0;
            long memAvailable = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    memTotal = ParseMemInfoValue(line);
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    memAvailable = ParseMemInfoValue(line);
                }
            }

            var memUsed = memTotal - memAvailable;
            return (memUsed * 1024, memTotal * 1024); // Convert from KB to bytes
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error reading memory info from /proc/meminfo");
            return (0, 0);
        }
    }

    private static long ParseMemInfoValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out var value))
        {
            return value;
        }
        return 0;
    }

    private (long used, long total) GetDiskUsage()
    {
        try
        {
            var driveInfo = new DriveInfo("/");
            return (driveInfo.TotalSize - driveInfo.AvailableFreeSpace, driveInfo.TotalSize);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting disk usage");
            return (0, 0);
        }
    }
}

