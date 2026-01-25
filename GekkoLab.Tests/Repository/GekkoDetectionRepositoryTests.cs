using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Repository;

[TestClass]
public class GekkoDetectionRepositoryTests
{
    private GekkoLabDbContext _context = null!;
    private Mock<ILogger<GekkoDetectionRepository>> _loggerMock = null!;
    private GekkoDetectionRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GekkoLabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GekkoLabDbContext(options);
        _loggerMock = new Mock<ILogger<GekkoDetectionRepository>>();
        _repository = new GekkoDetectionRepository(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveAsync_SavesDetectionToDatabase()
    {
        // Arrange
        var detection = new GekkoDetectionResult
        {
            Timestamp = DateTime.UtcNow,
            ImagePath = "/path/to/image.jpg",
            GekkoDetected = true,
            Confidence = 0.85f,
            Label = "gecko"
        };

        // Act
        await _repository.SaveAsync(detection);

        // Assert
        var savedDetections = await _context.GekkoDetections.ToListAsync();
        savedDetections.Should().HaveCount(1);
        savedDetections[0].GekkoDetected.Should().BeTrue();
        savedDetections[0].Confidence.Should().Be(0.85f);
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsLatestDetection()
    {
        // Arrange
        var detection1 = new GekkoDetectionResult
        {
            Timestamp = DateTime.UtcNow.AddHours(-2),
            ImagePath = "img1.jpg",
            GekkoDetected = false,
            Confidence = 0.30f
        };

        var detection2 = new GekkoDetectionResult
        {
            Timestamp = DateTime.UtcNow,
            ImagePath = "img2.jpg",
            GekkoDetected = true,
            Confidence = 0.90f
        };

        await _context.GekkoDetections.AddRangeAsync(detection1, detection2);
        await _context.SaveChangesAsync();

        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().NotBeNull();
        latest!.Confidence.Should().Be(0.90f);
        latest.ImagePath.Should().Be("img2.jpg");
    }

    [TestMethod]
    public async Task GetLatestAsync_WithNoDetections_ReturnsNull()
    {
        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().BeNull();
    }

    [TestMethod]
    public async Task GetLatestWithGekkoAsync_ReturnsLatestGekkoDetection()
    {
        // Arrange
        var detections = new[]
        {
            new GekkoDetectionResult { Timestamp = DateTime.UtcNow.AddHours(-3), ImagePath = "img1.jpg", GekkoDetected = true, Confidence = 0.80f },
            new GekkoDetectionResult { Timestamp = DateTime.UtcNow.AddHours(-2), ImagePath = "img2.jpg", GekkoDetected = false, Confidence = 0.20f },
            new GekkoDetectionResult { Timestamp = DateTime.UtcNow.AddHours(-1), ImagePath = "img3.jpg", GekkoDetected = true, Confidence = 0.95f },
            new GekkoDetectionResult { Timestamp = DateTime.UtcNow, ImagePath = "img4.jpg", GekkoDetected = false, Confidence = 0.15f }
        };

        await _context.GekkoDetections.AddRangeAsync(detections);
        await _context.SaveChangesAsync();

        // Act
        var latestGekko = await _repository.GetLatestWithGekkoAsync();

        // Assert
        latestGekko.Should().NotBeNull();
        latestGekko!.ImagePath.Should().Be("img3.jpg");
        latestGekko.Confidence.Should().Be(0.95f);
    }

    [TestMethod]
    public async Task GetLatestWithGekkoAsync_WithNoGekkoDetections_ReturnsNull()
    {
        // Arrange
        var detection = new GekkoDetectionResult
        {
            Timestamp = DateTime.UtcNow,
            ImagePath = "img.jpg",
            GekkoDetected = false,
            Confidence = 0.10f
        };

        await _context.GekkoDetections.AddAsync(detection);
        await _context.SaveChangesAsync();

        // Act
        var latestGekko = await _repository.GetLatestWithGekkoAsync();

        // Assert
        latestGekko.Should().BeNull();
    }

    [TestMethod]
    public async Task GetHistoryAsync_ReturnsDetectionsInDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var detections = new[]
        {
            new GekkoDetectionResult { Timestamp = now.AddHours(-5), ImagePath = "img1.jpg", GekkoDetected = false, Confidence = 0.20f },
            new GekkoDetectionResult { Timestamp = now.AddHours(-3), ImagePath = "img2.jpg", GekkoDetected = true, Confidence = 0.80f },
            new GekkoDetectionResult { Timestamp = now.AddHours(-1), ImagePath = "img3.jpg", GekkoDetected = false, Confidence = 0.25f }
        };

        await _context.GekkoDetections.AddRangeAsync(detections);
        await _context.SaveChangesAsync();

        // Act
        var history = await _repository.GetHistoryAsync(now.AddHours(-4), now.AddMinutes(-30));

        // Assert
        history.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GetDetectionsWithGekkoAsync_ReturnsOnlyGekkoDetections()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var detections = new[]
        {
            new GekkoDetectionResult { Timestamp = now.AddHours(-3), ImagePath = "img1.jpg", GekkoDetected = false, Confidence = 0.20f },
            new GekkoDetectionResult { Timestamp = now.AddHours(-2), ImagePath = "img2.jpg", GekkoDetected = true, Confidence = 0.80f },
            new GekkoDetectionResult { Timestamp = now.AddHours(-1), ImagePath = "img3.jpg", GekkoDetected = true, Confidence = 0.90f },
            new GekkoDetectionResult { Timestamp = now, ImagePath = "img4.jpg", GekkoDetected = false, Confidence = 0.15f }
        };

        await _context.GekkoDetections.AddRangeAsync(detections);
        await _context.SaveChangesAsync();

        // Act
        var gekkoDetections = await _repository.GetDetectionsWithGekkoAsync(now.AddHours(-4), now.AddMinutes(1));

        // Assert
        gekkoDetections.Should().HaveCount(2);
        gekkoDetections.All(d => d.GekkoDetected).Should().BeTrue();
    }

    [TestMethod]
    public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var detections = new[]
        {
            new GekkoDetectionResult { Timestamp = now.AddHours(-2), ImagePath = "img1.jpg", GekkoDetected = false, Confidence = 0.20f },
            new GekkoDetectionResult { Timestamp = now.AddHours(-1), ImagePath = "img2.jpg", GekkoDetected = true, Confidence = 0.80f },
            new GekkoDetectionResult { Timestamp = now.AddMinutes(-30), ImagePath = "img3.jpg", GekkoDetected = true, Confidence = 0.90f },
            new GekkoDetectionResult { Timestamp = now, ImagePath = "img4.jpg", GekkoDetected = false, Confidence = 0.10f }
        };

        await _context.GekkoDetections.AddRangeAsync(detections);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _repository.GetStatisticsAsync(now.AddHours(-3), now.AddMinutes(1));

        // Assert
        stats.TotalDetections.Should().Be(4);
        stats.GekkoDetections.Should().Be(2);
        stats.GekkoDetectionRate.Should().Be(0.5);
        stats.AverageConfidence.Should().BeApproximately(0.50f, 0.01f);
    }

    [TestMethod]
    public async Task GetStatisticsAsync_WithNoDetections_ReturnsZeroStats()
    {
        // Act
        var stats = await _repository.GetStatisticsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        // Assert
        stats.TotalDetections.Should().Be(0);
        stats.GekkoDetections.Should().Be(0);
        stats.GekkoDetectionRate.Should().Be(0);
        stats.AverageConfidence.Should().Be(0);
        stats.LastGekkoDetection.Should().BeNull();
    }

    [TestMethod]
    public async Task GetStatisticsAsync_SetsLastGekkoDetection()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var lastGekkoTime = now.AddMinutes(-15);

        var detections = new[]
        {
            new GekkoDetectionResult { Timestamp = now.AddHours(-2), ImagePath = "img1.jpg", GekkoDetected = true, Confidence = 0.80f },
            new GekkoDetectionResult { Timestamp = lastGekkoTime, ImagePath = "img2.jpg", GekkoDetected = true, Confidence = 0.90f },
            new GekkoDetectionResult { Timestamp = now, ImagePath = "img3.jpg", GekkoDetected = false, Confidence = 0.10f }
        };

        await _context.GekkoDetections.AddRangeAsync(detections);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _repository.GetStatisticsAsync(now.AddHours(-3), now.AddMinutes(1));

        // Assert
        stats.LastGekkoDetection.Should().BeCloseTo(lastGekkoTime, TimeSpan.FromSeconds(1));
    }
}
