using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GekkoLab.Tests.Repository;

[TestClass]
public class SensorReadingRepositoryTests
{
    private GekkoLabDbContext _context = null!;
    private SensorReadingRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GekkoLabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GekkoLabDbContext(options);
        _repository = new SensorReadingRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveReadingAsync_SavesReadingToDatabase()
    {
        // Arrange
        var reading = new SensorReading
        {
            Temperature = 25.0,
            Humidity = 60.0,
            Pressure = 760.0,
            Timestamp = DateTime.UtcNow,
            IsValid = true
        };

        // Act
        var result = await _repository.SaveReadingAsync(reading);

        // Assert
        result.Id.Should().BeGreaterThan(0);
        var savedReading = await _context.SensorReadings.FindAsync(result.Id);
        savedReading.Should().NotBeNull();
        savedReading!.Temperature.Should().Be(25.0);
    }

    [TestMethod]
    public async Task GetLatestReadingAsync_ReturnsLatestReading()
    {
        // Arrange
        var readings = new List<SensorReading>
        {
            new() { Temperature = 20.0, Humidity = 50.0, Pressure = 755.0, Timestamp = DateTime.UtcNow.AddHours(-2), IsValid = true },
            new() { Temperature = 25.0, Humidity = 60.0, Pressure = 760.0, Timestamp = DateTime.UtcNow.AddHours(-1), IsValid = true },
            new() { Temperature = 22.0, Humidity = 55.0, Pressure = 758.0, Timestamp = DateTime.UtcNow, IsValid = true }
        };
        await _context.SensorReadings.AddRangeAsync(readings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestReadingAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Temperature.Should().Be(22.0);
    }

    [TestMethod]
    public async Task GetLatestReadingAsync_WhenNoReadings_ReturnsNull()
    {
        // Act
        var result = await _repository.GetLatestReadingAsync();

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task GetReadingsByDateRangeAsync_ReturnsReadingsInRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var readings = new List<SensorReading>
        {
            new() { Temperature = 20.0, Humidity = 50.0, Pressure = 755.0, Timestamp = now.AddDays(-5), IsValid = true },
            new() { Temperature = 22.0, Humidity = 55.0, Pressure = 758.0, Timestamp = now.AddDays(-2), IsValid = true },
            new() { Temperature = 25.0, Humidity = 60.0, Pressure = 760.0, Timestamp = now.AddDays(-1), IsValid = true }
        };
        await _context.SensorReadings.AddRangeAsync(readings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetReadingsByDateRangeAsync(now.AddDays(-3), now);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.Timestamp >= now.AddDays(-3));
    }

    [TestMethod]
    public async Task GetDailyAveragesAsync_ReturnsCorrectAveragesForTemperature()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var readings = new List<SensorReading>
        {
            new() { Temperature = 20.0, Humidity = 50.0, Pressure = 755.0, Timestamp = today.AddHours(8), IsValid = true },
            new() { Temperature = 24.0, Humidity = 55.0, Pressure = 758.0, Timestamp = today.AddHours(12), IsValid = true },
            new() { Temperature = 22.0, Humidity = 60.0, Pressure = 760.0, Timestamp = today.AddHours(16), IsValid = true }
        };
        await _context.SensorReadings.AddRangeAsync(readings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDailyAveragesAsync("temperature", today, today.AddDays(1));

        // Assert
        result.Should().ContainKey(today);
        result[today].Should().Be(22.0); // (20 + 24 + 22) / 3
    }

    [TestMethod]
    public async Task GetDailyAveragesAsync_ReturnsCorrectAveragesForHumidity()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var readings = new List<SensorReading>
        {
            new() { Temperature = 20.0, Humidity = 50.0, Pressure = 755.0, Timestamp = today.AddHours(8), IsValid = true },
            new() { Temperature = 24.0, Humidity = 60.0, Pressure = 758.0, Timestamp = today.AddHours(12), IsValid = true }
        };
        await _context.SensorReadings.AddRangeAsync(readings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDailyAveragesAsync("humidity", today, today.AddDays(1));

        // Assert
        result.Should().ContainKey(today);
        result[today].Should().Be(55.0); // (50 + 60) / 2
    }

    [TestMethod]
    public async Task GetDailyAveragesAsync_WithInvalidMetric_ReturnsEmptyDictionary()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var reading = new SensorReading
        {
            Temperature = 20.0,
            Humidity = 50.0,
            Pressure = 755.0,
            Timestamp = today.AddHours(8),
            IsValid = true
        };
        await _context.SensorReadings.AddAsync(reading);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDailyAveragesAsync("invalid_metric", today, today.AddDays(1));

        // Assert
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetTotalReadingsCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var readings = new List<SensorReading>
        {
            new() { Temperature = 20.0, Humidity = 50.0, Pressure = 755.0, Timestamp = DateTime.UtcNow, IsValid = true },
            new() { Temperature = 22.0, Humidity = 55.0, Pressure = 758.0, Timestamp = DateTime.UtcNow, IsValid = true },
            new() { Temperature = 24.0, Humidity = 60.0, Pressure = 760.0, Timestamp = DateTime.UtcNow, IsValid = true }
        };
        await _context.SensorReadings.AddRangeAsync(readings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTotalReadingsCountAsync();

        // Assert
        result.Should().Be(3);
    }
}
