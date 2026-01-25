using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Repository;

[TestClass]
public class SystemMetricsRepositoryTests
{
    private GekkoLabDbContext _context = null!;
    private Mock<ILogger<SystemMetricsRepository>> _loggerMock = null!;
    private SystemMetricsRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GekkoLabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GekkoLabDbContext(options);
        _loggerMock = new Mock<ILogger<SystemMetricsRepository>>();
        _repository = new SystemMetricsRepository(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveMetricsAsync_SavesMetricsToDatabase()
    {
        // Arrange
        var metrics = new SystemMetrics
        {
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = 25.5,
            MemoryUsagePercent = 60.0,
            DiskUsagePercent = 45.0,
            MemoryTotalBytes = 8_000_000_000,
            MemoryUsedBytes = 4_800_000_000,
            DiskTotalBytes = 500_000_000_000,
            DiskUsedBytes = 225_000_000_000
        };

        // Act
        await _repository.SaveMetricsAsync(metrics);

        // Assert
        var savedMetrics = await _context.SystemMetrics.ToListAsync();
        savedMetrics.Should().HaveCount(1);
        savedMetrics[0].CpuUsagePercent.Should().Be(25.5);
        savedMetrics[0].MemoryUsagePercent.Should().Be(60.0);
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsLatestMetrics()
    {
        // Arrange
        var metrics1 = new SystemMetrics
        {
            Timestamp = DateTime.UtcNow.AddHours(-2),
            CpuUsagePercent = 20.0,
            MemoryUsagePercent = 50.0,
            DiskUsagePercent = 40.0
        };

        var metrics2 = new SystemMetrics
        {
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = 30.0,
            MemoryUsagePercent = 65.0,
            DiskUsagePercent = 45.0
        };

        await _context.SystemMetrics.AddRangeAsync(metrics1, metrics2);
        await _context.SaveChangesAsync();

        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().NotBeNull();
        latest!.CpuUsagePercent.Should().Be(30.0);
    }

    [TestMethod]
    public async Task GetLatestAsync_WithNoMetrics_ReturnsNull()
    {
        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().BeNull();
    }

    [TestMethod]
    public async Task GetByDateRangeAsync_ReturnsMetricsInDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics = new[]
        {
            new SystemMetrics { Timestamp = now.AddHours(-5), CpuUsagePercent = 20.0, MemoryUsagePercent = 50.0, DiskUsagePercent = 40.0 },
            new SystemMetrics { Timestamp = now.AddHours(-3), CpuUsagePercent = 25.0, MemoryUsagePercent = 55.0, DiskUsagePercent = 42.0 },
            new SystemMetrics { Timestamp = now.AddHours(-1), CpuUsagePercent = 30.0, MemoryUsagePercent = 60.0, DiskUsagePercent = 44.0 }
        };

        await _context.SystemMetrics.AddRangeAsync(metrics);
        await _context.SaveChangesAsync();

        // Act
        var history = await _repository.GetByDateRangeAsync(now.AddHours(-4), now.AddMinutes(-30));

        // Assert
        history.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GetByDateRangeAsync_ReturnsMetricsOrderedByTimestamp()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics = new[]
        {
            new SystemMetrics { Timestamp = now.AddHours(-1), CpuUsagePercent = 30.0, MemoryUsagePercent = 60.0, DiskUsagePercent = 44.0 },
            new SystemMetrics { Timestamp = now.AddHours(-3), CpuUsagePercent = 25.0, MemoryUsagePercent = 55.0, DiskUsagePercent = 42.0 },
            new SystemMetrics { Timestamp = now.AddHours(-2), CpuUsagePercent = 27.0, MemoryUsagePercent = 57.0, DiskUsagePercent = 43.0 }
        };

        await _context.SystemMetrics.AddRangeAsync(metrics);
        await _context.SaveChangesAsync();

        // Act
        var history = (await _repository.GetByDateRangeAsync(now.AddHours(-4), now)).ToList();

        // Assert
        history.Should().HaveCount(3);
        history[0].CpuUsagePercent.Should().Be(25.0); // Oldest first
        history[1].CpuUsagePercent.Should().Be(27.0);
        history[2].CpuUsagePercent.Should().Be(30.0); // Newest last
    }

    [TestMethod]
    public async Task CleanupOldMetricsAsync_RemovesOldMetrics()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics = new[]
        {
            new SystemMetrics { Timestamp = now.AddDays(-10), CpuUsagePercent = 20.0, MemoryUsagePercent = 50.0, DiskUsagePercent = 40.0 },
            new SystemMetrics { Timestamp = now.AddDays(-8), CpuUsagePercent = 25.0, MemoryUsagePercent = 55.0, DiskUsagePercent = 42.0 },
            new SystemMetrics { Timestamp = now.AddDays(-1), CpuUsagePercent = 30.0, MemoryUsagePercent = 60.0, DiskUsagePercent = 44.0 }
        };

        await _context.SystemMetrics.AddRangeAsync(metrics);
        await _context.SaveChangesAsync();

        // Act
        await _repository.CleanupOldMetricsAsync(7);

        // Assert
        var remainingMetrics = await _context.SystemMetrics.ToListAsync();
        remainingMetrics.Should().HaveCount(1);
        remainingMetrics[0].CpuUsagePercent.Should().Be(30.0);
    }

    [TestMethod]
    public async Task CleanupOldMetricsAsync_WithNoOldMetrics_DoesNothing()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics = new SystemMetrics
        {
            Timestamp = now.AddHours(-1),
            CpuUsagePercent = 30.0,
            MemoryUsagePercent = 60.0,
            DiskUsagePercent = 44.0
        };

        await _context.SystemMetrics.AddAsync(metrics);
        await _context.SaveChangesAsync();

        // Act
        await _repository.CleanupOldMetricsAsync(7);

        // Assert
        var remainingMetrics = await _context.SystemMetrics.ToListAsync();
        remainingMetrics.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GetTotalCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var metrics = new[]
        {
            new SystemMetrics { Timestamp = DateTime.UtcNow.AddHours(-2), CpuUsagePercent = 20.0, MemoryUsagePercent = 50.0, DiskUsagePercent = 40.0 },
            new SystemMetrics { Timestamp = DateTime.UtcNow.AddHours(-1), CpuUsagePercent = 25.0, MemoryUsagePercent = 55.0, DiskUsagePercent = 42.0 },
            new SystemMetrics { Timestamp = DateTime.UtcNow, CpuUsagePercent = 30.0, MemoryUsagePercent = 60.0, DiskUsagePercent = 44.0 }
        };

        await _context.SystemMetrics.AddRangeAsync(metrics);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetTotalCountAsync();

        // Assert
        count.Should().Be(3);
    }
}
