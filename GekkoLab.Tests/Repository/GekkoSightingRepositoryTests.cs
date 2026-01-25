using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Repository;

[TestClass]
public class GekkoSightingRepositoryTests
{
    private GekkoLabDbContext _context = null!;
    private Mock<ILogger<GekkoSightingRepository>> _loggerMock = null!;
    private GekkoSightingRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GekkoLabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GekkoLabDbContext(options);
        _loggerMock = new Mock<ILogger<GekkoSightingRepository>>();
        _repository = new GekkoSightingRepository(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveAsync_SavesSightingToDatabase()
    {
        // Arrange
        var sighting = new GekkoSighting
        {
            Timestamp = DateTime.UtcNow,
            ImagePath = "/path/to/image.jpg",
            Confidence = 0.85f,
            Label = "gecko",
            PositionX = 100,
            PositionY = 200
        };

        // Act
        await _repository.SaveAsync(sighting);

        // Assert
        var savedSightings = await _context.GekkoSightings.ToListAsync();
        savedSightings.Should().HaveCount(1);
        savedSightings[0].Confidence.Should().Be(0.85f);
        savedSightings[0].Label.Should().Be("gecko");
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsLatestSighting()
    {
        // Arrange
        var sighting1 = new GekkoSighting
        {
            Timestamp = DateTime.UtcNow.AddHours(-2),
            ImagePath = "/path/to/image1.jpg",
            Confidence = 0.75f
        };

        var sighting2 = new GekkoSighting
        {
            Timestamp = DateTime.UtcNow,
            ImagePath = "/path/to/image2.jpg",
            Confidence = 0.90f
        };

        await _context.GekkoSightings.AddRangeAsync(sighting1, sighting2);
        await _context.SaveChangesAsync();

        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().NotBeNull();
        latest!.Confidence.Should().Be(0.90f);
    }

    [TestMethod]
    public async Task GetLatestAsync_WithNoSightings_ReturnsNull()
    {
        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().BeNull();
    }

    [TestMethod]
    public async Task GetHistoryAsync_ReturnsSightingsInDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sightings = new[]
        {
            new GekkoSighting { Timestamp = now.AddHours(-5), ImagePath = "img1.jpg", Confidence = 0.80f },
            new GekkoSighting { Timestamp = now.AddHours(-3), ImagePath = "img2.jpg", Confidence = 0.85f },
            new GekkoSighting { Timestamp = now.AddHours(-1), ImagePath = "img3.jpg", Confidence = 0.90f }
        };

        await _context.GekkoSightings.AddRangeAsync(sightings);
        await _context.SaveChangesAsync();

        // Act
        var history = await _repository.GetHistoryAsync(now.AddHours(-4), now.AddMinutes(-30));

        // Assert
        history.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sightings = new[]
        {
            new GekkoSighting { Timestamp = now.AddHours(-2), ImagePath = "img1.jpg", Confidence = 0.80f },
            new GekkoSighting { Timestamp = now.AddHours(-1), ImagePath = "img2.jpg", Confidence = 0.85f },
            new GekkoSighting { Timestamp = now, ImagePath = "img3.jpg", Confidence = 0.90f }
        };

        await _context.GekkoSightings.AddRangeAsync(sightings);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetCountAsync(now.AddHours(-3), now.AddMinutes(1));

        // Assert
        count.Should().Be(3);
    }

    [TestMethod]
    public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sightings = new[]
        {
            new GekkoSighting { Timestamp = now.AddHours(-25), ImagePath = "img1.jpg", Confidence = 0.70f }, // Outside 24h
            new GekkoSighting { Timestamp = now.AddHours(-2), ImagePath = "img2.jpg", Confidence = 0.80f },  // Within 24h
            new GekkoSighting { Timestamp = now.AddMinutes(-30), ImagePath = "img3.jpg", Confidence = 0.90f }, // Within 1h
            new GekkoSighting { Timestamp = now.AddMinutes(-15), ImagePath = "img4.jpg", Confidence = 1.00f }  // Within 1h
        };

        await _context.GekkoSightings.AddRangeAsync(sightings);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _repository.GetStatisticsAsync(now.AddDays(-2), now.AddMinutes(1));

        // Assert
        stats.TotalSightings.Should().Be(4);
        stats.SightingsLast24Hours.Should().Be(3);
        stats.SightingsLastHour.Should().Be(2);
        stats.AverageConfidence.Should().BeApproximately(0.85f, 0.01f);
        stats.MaxConfidence.Should().Be(1.00f);
    }

    [TestMethod]
    public async Task GetStatisticsAsync_WithNoSightings_ReturnsZeroStats()
    {
        // Act
        var stats = await _repository.GetStatisticsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        // Assert
        stats.TotalSightings.Should().Be(0);
        stats.SightingsLast24Hours.Should().Be(0);
        stats.SightingsLastHour.Should().Be(0);
        stats.AverageConfidence.Should().Be(0);
        stats.MaxConfidence.Should().Be(0);
        stats.FirstSighting.Should().BeNull();
        stats.LastSighting.Should().BeNull();
    }

    [TestMethod]
    public async Task GetStatisticsAsync_SetsFirstAndLastSighting()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var firstTime = now.AddHours(-5);
        var lastTime = now.AddMinutes(-10);

        var sightings = new[]
        {
            new GekkoSighting { Timestamp = firstTime, ImagePath = "img1.jpg", Confidence = 0.80f },
            new GekkoSighting { Timestamp = now.AddHours(-2), ImagePath = "img2.jpg", Confidence = 0.85f },
            new GekkoSighting { Timestamp = lastTime, ImagePath = "img3.jpg", Confidence = 0.90f }
        };

        await _context.GekkoSightings.AddRangeAsync(sightings);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _repository.GetStatisticsAsync(now.AddDays(-1), now);

        // Assert
        stats.FirstSighting.Should().BeCloseTo(firstTime, TimeSpan.FromSeconds(1));
        stats.LastSighting.Should().BeCloseTo(lastTime, TimeSpan.FromSeconds(1));
    }
}
