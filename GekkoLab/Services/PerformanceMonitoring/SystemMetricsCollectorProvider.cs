namespace GekkoLab.Services.PerformanceMonitoring;

/// <summary>
/// Provider for system metrics collector
/// </summary>
public interface ISystemMetricsCollectorProvider
{
    ISystemMetricsCollector GetCollector();
}

public class SystemMetricsCollectorProvider : ISystemMetricsCollectorProvider
{
    private readonly Lazy<ISystemMetricsCollector> _collector;

    public SystemMetricsCollectorProvider(ILoggerFactory loggerFactory)
    {
        _collector = new Lazy<ISystemMetricsCollector>(() =>
        {
            var logger = loggerFactory.CreateLogger<SystemMetricsCollectorProvider>();
            logger.LogInformation("Using Linux System Metrics Collector");
            return new LinuxSystemMetricsCollector(loggerFactory.CreateLogger<LinuxSystemMetricsCollector>());
        });
    }

    public ISystemMetricsCollector GetCollector() => _collector.Value;
}
