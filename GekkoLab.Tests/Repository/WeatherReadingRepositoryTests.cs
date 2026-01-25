using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Repository;

[TestClass]
public class WeatherReadingRepositoryTests
{
    private GekkoLabDbContext _context = null!;
    private Mock<ILogger<WeatherReadingRepository>> _loggerMock = null!;
    private WeatherReadingRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GekkoLabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GekkoLabDbContext(options);
        _loggerMock = new Mock<ILogger<WeatherReadingRepository>>();
        _repository = new WeatherReadingRepository(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveAsync_SavesReadingToDatabase()
    {
        // Arrange
        var reading = new WeatherReading
        {
            Timestamp = DateTime.UtcNow,
            Temperature = 15.5,
            Humidity = 70.0,
            Latitude = 47.67,
            Longitude = -122.12,
            Location = "Redmond",
            Source = "Open-Meteo"
        };

        // Act
        await _repository.SaveAsync(reading);

        // Assert
        var savedReadings = await _context.WeatherReadings.ToListAsync();
        savedReadings.Should().HaveCount(1);
        savedReadings[0].Temperature.Should().Be(15.5);
        savedReadings[0].Humidity.Should().Be(70.0);
        savedReadings[0].Location.Should().Be("Redmond");
    }

    [TestMethod]
    public async Task GetLatestAsync_ReturnsLatestReading()
    {
        // Arrange
        var reading1 = new WeatherReading
        {
            Timestamp = DateTime.UtcNow.AddHours(-2),
            Temperature = 10.0,
            Humidity = 65.0,
            Location = "Redmond",
            Source = "Open-Meteo"
        };

        var reading2 = new WeatherReading
        {
            Timestamp = DateTime.UtcNow,
            Temperature = 15.5,
            Humidity = 70.0,
            Location = "Redmond",
            Source = "Open-Meteo"
        };

        await _context.WeatherReadings.AddRangeAsync(reading1, reading2);
        await _context.SaveChangesAsync();

        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().NotBeNull();
        latest!.Temperature.Should().Be(15.5);
    }

    [TestMethod]
    public async Task GetLatestAsync_WithNoReadings_ReturnsNull()
    {
        // Act
        var latest = await _repository.GetLatestAsync();

        // Assert
        latest.Should().BeNull();
    }

    [TestMethod]
    public async Task GetHistoryAsync_ReturnsReadingsInDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var readings = new[]
        {
            new WeatherReading { Timestamp = now.AddHours(-5), Temperature = 10.0, Humidity = 60.0, Location = "Redmond", Source = "Open-Meteo" },
            new WeatherReading { Timestamp = now.AddHours(-3), Temperature = 12.0, Humidity = 62.0, Location = "Redmond", Source = "Open-Meteo" },
            new WeatherReading { Timestamp = now.AddHours(-1), Temperature = 14.0, Humidity = 64.0, Location = "Redmond", Source = "Open-Meteo" },
            new WeatherReading { Timestamp = now, Temperature = 15.0, Humidity = 65.0, Location = "Redmond", Source = "Open-Meteo" }
        };

        await _context.WeatherReadings.AddRangeAsync(readings);
        await _context.SaveChangesAsync();

        // Act
        var history = await _repository.GetHistoryAsync(now.AddHours(-4), now.AddMinutes(-30));

        // Assert
        history.Should().HaveCount(2);
        history.Select(r => r.Temperature).Should().BeEquivalentTo(new[] { 12.0, 14.0 });
    }

    [TestMethod]
    public async Task GetHistoryAsync_WithNoMatchingReadings_ReturnsEmptyList()
    {
        // Arrange
        var reading = new WeatherReading
        {
            Timestamp = DateTime.UtcNow.AddDays(-10),
            Temperature = 10.0,
            Humidity = 60.0,
            Location = "Redmond",
            Source = "Open-Meteo"
        };

        await _context.WeatherReadings.AddAsync(reading);
        await _context.SaveChangesAsync();

        // Act
        var history = await _repository.GetHistoryAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        // Assert
        history.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetHistoryAsync_ReturnsReadingsOrderedByTimestamp()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var readings = new[]
        {
            new WeatherReading { Timestamp = now.AddHours(-1), Temperature = 14.0, Humidity = 64.0, Location = "Redmond", Source = "Open-Meteo" },
            new WeatherReading { Timestamp = now.AddHours(-3), Temperature = 12.0, Humidity = 62.0, Location = "Redmond", Source = "Open-Meteo" },
            new WeatherReading { Timestamp = now.AddHours(-2), Temperature = 13.0, Humidity = 63.0, Location = "Redmond", Source = "Open-Meteo" }
        };

        await _context.WeatherReadings.AddRangeAsync(readings);
        await _context.SaveChangesAsync();

        // Act
        var history = (await _repository.GetHistoryAsync(now.AddHours(-4), now)).ToList();

        // Assert
        history.Should().HaveCount(3);
        history[0].Temperature.Should().Be(12.0); // Oldest first
        history[1].Temperature.Should().Be(13.0);
        history[2].Temperature.Should().Be(14.0); // Newest last
    }

    [TestMethod]
    public async Task SaveAsync_MultipleSaves_AllPersisted()
    {
        // Arrange & Act
        for (int i = 0; i < 5; i++)
        {
            var reading = new WeatherReading
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                Temperature = 10.0 + i,
                Humidity = 60.0 + i,
                Location = "Redmond",
                Source = "Open-Meteo"
            };
            await _repository.SaveAsync(reading);
        }

        // Assert
        var allReadings = await _context.WeatherReadings.ToListAsync();
        allReadings.Should().HaveCount(5);
    }
}
