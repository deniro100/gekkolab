using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services.PerformanceMonitoring;
using GekkoLab.Services.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class PerformanceMonitoringServiceTests
{
    private Mock<ILogger<PerformanceMonitoringService>> _loggerMock = null!;
    private Mock<IMetricsStore> _metricsStoreMock = null!;
    private Mock<ISystemMetricsCollector> _collectorMock = null!;
    private Mock<ISystemMetricsCollectorProvider> _collectorProviderMock = null!;
    private Mock<ISystemMetricsRepository> _repositoryMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<IServiceScope> _scopeMock = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<PerformanceMonitoringService>>();
        _metricsStoreMock = new Mock<IMetricsStore>();
        _collectorMock = new Mock<ISystemMetricsCollector>();
        _collectorProviderMock = new Mock<ISystemMetricsCollectorProvider>();
        _repositoryMock = new Mock<ISystemMetricsRepository>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _collectorProviderMock
            .Setup(p => p.GetCollector())
            .Returns(_collectorMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ISystemMetricsRepository)))
            .Returns(_repositoryMock.Object);

        _scopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);
    }

    private IConfiguration CreateConfiguration(bool enabled = true, string snapshotInterval = "00:00:01", string aggregationInterval = "00:00:05")
    {
        var configData = new Dictionary<string, string?>
        {
            { "PerformanceMonitoring:Enabled", enabled.ToString() },
            { "PerformanceMonitoring:SnapshotInterval", snapshotInterval },
            { "PerformanceMonitoring:AggregationInterval", aggregationInterval },
            { "PerformanceMonitoring:MaxAgeDays", "7" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenDisabled_DoesNotCollectMetrics()
    {
        // Arrange
        var config = CreateConfiguration(enabled: false);
        var service = new PerformanceMonitoringService(
            _loggerMock.Object,
            config,
            _collectorProviderMock.Object,
            _metricsStoreMock.Object,
            _scopeFactoryMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _collectorMock.Verify(c => c.CollectMetricsAsync(), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenEnabled_CollectsMetrics()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, snapshotInterval: "00:00:01");
        
        _collectorMock
            .Setup(c => c.CollectMetricsAsync())
            .ReturnsAsync(new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = 25.0,
                MemoryUsagePercent = 60.0,
                DiskUsagePercent = 45.0
            });

        _metricsStoreMock
            .Setup(s => s.GetSnapshotsForAggregation())
            .Returns(new List<MetricsSnapshot>());

        var service = new PerformanceMonitoringService(
            _loggerMock.Object,
            config,
            _collectorProviderMock.Object,
            _metricsStoreMock.Object,
            _scopeFactoryMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _collectorMock.Verify(c => c.CollectMetricsAsync(), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_AddsSnapshotsToStore()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, snapshotInterval: "00:00:01");
        
        var snapshot = new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = 25.0,
            MemoryUsagePercent = 60.0,
            DiskUsagePercent = 45.0
        };

        _collectorMock
            .Setup(c => c.CollectMetricsAsync())
            .ReturnsAsync(snapshot);

        _metricsStoreMock
            .Setup(s => s.GetSnapshotsForAggregation())
            .Returns(new List<MetricsSnapshot>());

        var service = new PerformanceMonitoringService(
            _loggerMock.Object,
            config,
            _collectorProviderMock.Object,
            _metricsStoreMock.Object,
            _scopeFactoryMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _metricsStoreMock.Verify(s => s.AddSnapshot(It.IsAny<MetricsSnapshot>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_AggregatesAndSavesMetrics()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, snapshotInterval: "00:00:01", aggregationInterval: "00:00:02");
        
        var snapshots = new List<MetricsSnapshot>
        {
            new MetricsSnapshot { Timestamp = DateTime.UtcNow, CpuUsagePercent = 20.0, MemoryUsagePercent = 50.0, DiskUsagePercent = 40.0 },
            new MetricsSnapshot { Timestamp = DateTime.UtcNow, CpuUsagePercent = 30.0, MemoryUsagePercent = 60.0, DiskUsagePercent = 50.0 }
        };

        _collectorMock
            .Setup(c => c.CollectMetricsAsync())
            .ReturnsAsync(new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = 25.0,
                MemoryUsagePercent = 55.0,
                DiskUsagePercent = 45.0
            });

        _metricsStoreMock
            .Setup(s => s.GetSnapshotsForAggregation())
            .Returns(snapshots);

        var service = new PerformanceMonitoringService(
            _loggerMock.Object,
            config,
            _collectorProviderMock.Object,
            _metricsStoreMock.Object,
            _scopeFactoryMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(4));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveMetricsAsync(It.IsAny<SystemMetrics>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlesCollectorException()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, snapshotInterval: "00:00:01");
        var callCount = 0;

        _collectorMock
            .Setup(c => c.CollectMetricsAsync())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new Exception("Test exception");
                }
                return new MetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    CpuUsagePercent = 25.0,
                    MemoryUsagePercent = 60.0,
                    DiskUsagePercent = 45.0
                };
            });

        _metricsStoreMock
            .Setup(s => s.GetSnapshotsForAggregation())
            .Returns(new List<MetricsSnapshot>());

        var service = new PerformanceMonitoringService(
            _loggerMock.Object,
            config,
            _collectorProviderMock.Object,
            _metricsStoreMock.Object,
            _scopeFactoryMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(4));
        await service.StopAsync(CancellationToken.None);

        // Assert - should continue after exception
        callCount.Should().BeGreaterThan(1);
    }

    [TestMethod]
    public void Constructor_SetsDefaultIntervals_WhenNotConfigured()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "PerformanceMonitoring:Enabled", "true" }
            })
            .Build();

        // Act - should not throw
        var service = new PerformanceMonitoringService(
            _loggerMock.Object,
            emptyConfig,
            _collectorProviderMock.Object,
            _metricsStoreMock.Object,
            _scopeFactoryMock.Object);

        // Assert
        service.Should().NotBeNull();
    }
}
