using FluentAssertions;
using GekkoLab.Services.PerformanceMonitoring;

namespace GekkoLab.Tests.Services;

[TestClass]
public class InMemoryMetricsStoreTests
{
    private InMemoryMetricsStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new InMemoryMetricsStore();
    }

    [TestMethod]
    public void AddSnapshot_ShouldStoreSnapshot()
    {
        // Arrange
        var snapshot = CreateSnapshot(DateTime.UtcNow);

        // Act
        _store.AddSnapshot(snapshot);

        // Assert
        var snapshots = _store.GetSnapshots();
        snapshots.Should().HaveCount(1);
        snapshots[0].CpuUsagePercent.Should().Be(snapshot.CpuUsagePercent);
    }

    [TestMethod]
    public void GetLatest_WhenEmpty_ShouldReturnNull()
    {
        // Act
        var result = _store.GetLatest();

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GetLatest_ShouldReturnMostRecentSnapshot()
    {
        // Arrange
        var snapshot1 = CreateSnapshot(DateTime.UtcNow.AddMinutes(-5), 10);
        var snapshot2 = CreateSnapshot(DateTime.UtcNow.AddMinutes(-2), 20);
        var snapshot3 = CreateSnapshot(DateTime.UtcNow, 30);

        _store.AddSnapshot(snapshot1);
        _store.AddSnapshot(snapshot2);
        _store.AddSnapshot(snapshot3);

        // Act
        var result = _store.GetLatest();

        // Assert
        result.Should().NotBeNull();
        result!.CpuUsagePercent.Should().Be(30);
    }

    [TestMethod]
    public void GetSnapshots_WithDuration_ShouldFilterByTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _store.AddSnapshot(CreateSnapshot(now.AddMinutes(-10), 10));
        _store.AddSnapshot(CreateSnapshot(now.AddMinutes(-5), 20));
        _store.AddSnapshot(CreateSnapshot(now.AddMinutes(-2), 30));

        // Act
        var result = _store.GetSnapshots(TimeSpan.FromMinutes(6));

        // Assert
        result.Should().HaveCount(2);
        result[0].CpuUsagePercent.Should().Be(20);
        result[1].CpuUsagePercent.Should().Be(30);
    }

    [TestMethod]
    public void GetSnapshotsForAggregation_ShouldReturnAllSnapshotsAndClearCollection()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _store.AddSnapshot(CreateSnapshot(now.AddSeconds(-45), 10));
        _store.AddSnapshot(CreateSnapshot(now.AddSeconds(-30), 20));
        _store.AddSnapshot(CreateSnapshot(now.AddSeconds(-15), 30));

        // Act
        var result = _store.GetSnapshotsForAggregation();

        // Assert
        result.Should().HaveCount(3);
        result[0].CpuUsagePercent.Should().Be(10);
        result[1].CpuUsagePercent.Should().Be(20);
        result[2].CpuUsagePercent.Should().Be(30);
    }

    [TestMethod]
    public void GetSnapshotsForAggregation_ShouldSwapToNewCollection()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _store.AddSnapshot(CreateSnapshot(now.AddSeconds(-30), 10));
        _store.AddSnapshot(CreateSnapshot(now.AddSeconds(-15), 20));

        // Act - First aggregation
        var firstResult = _store.GetSnapshotsForAggregation();
        
        // Add new snapshot after aggregation
        _store.AddSnapshot(CreateSnapshot(now, 30));
        
        // Second aggregation should only have the new snapshot
        var secondResult = _store.GetSnapshotsForAggregation();

        // Assert
        firstResult.Should().HaveCount(2);
        secondResult.Should().HaveCount(1);
        secondResult[0].CpuUsagePercent.Should().Be(30);
    }

    [TestMethod]
    public void GetSnapshots_ShouldStillReturnDataAfterAggregation()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _store.AddSnapshot(CreateSnapshot(now.AddSeconds(-30), 10));
        _store.AddSnapshot(CreateSnapshot(now.AddSeconds(-15), 20));

        // Act - Aggregate (this clears aggregation collection but not display collection)
        _store.GetSnapshotsForAggregation();
        
        // GetSnapshots should still return data for display
        var displaySnapshots = _store.GetSnapshots();

        // Assert
        displaySnapshots.Should().HaveCount(2);
    }

    private static MetricsSnapshot CreateSnapshot(DateTime timestamp, double cpuUsage = 25.0)
    {
        return new MetricsSnapshot
        {
            CpuUsagePercent = cpuUsage,
            MemoryUsagePercent = 50.0,
            DiskUsagePercent = 60.0,
            MemoryUsedBytes = 2L * 1024 * 1024 * 1024,
            MemoryTotalBytes = 4L * 1024 * 1024 * 1024,
            DiskUsedBytes = 16L * 1024 * 1024 * 1024,
            DiskTotalBytes = 32L * 1024 * 1024 * 1024,
            Timestamp = timestamp
        };
    }
}
