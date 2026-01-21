using FluentAssertions;
using GekkoLab.Controllers;
using GekkoLab.Models;
using GekkoLab.Services.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GekkoLab.Tests.Controllers;

[TestClass]
public class SensorControllerTests
{
    private Mock<ISensorReadingRepository> _repositoryMock = null!;
    private Mock<ILogger<SensorController>> _loggerMock = null!;
    private SensorController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _repositoryMock = new Mock<ISensorReadingRepository>();
        _loggerMock = new Mock<ILogger<SensorController>>();
        _controller = new SensorController(_repositoryMock.Object, _loggerMock.Object);
    }

    [TestMethod]
    public async Task GetLatest_WhenReadingExists_ReturnsOkWithReading()
    {
        // Arrange
        var expectedReading = new SensorReading
        {
            Id = 1,
            Temperature = 25.5,
            Humidity = 60.0,
            Pressure = 760.0,
            Timestamp = DateTime.UtcNow,
            IsValid = true
        };
        _repositoryMock.Setup(r => r.GetLatestReadingAsync())
            .ReturnsAsync(expectedReading);

        // Act
        var result = await _controller.GetLatest();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedReading);
    }

    [TestMethod]
    public async Task GetLatest_WhenNoReadingExists_ReturnsNotFound()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetLatestReadingAsync())
            .ReturnsAsync((SensorReading?)null);

        // Act
        var result = await _controller.GetLatest();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task GetHistory_WithDateRange_ReturnsReadingsInRange()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-3);
        var to = DateTime.UtcNow;
        var expectedReadings = new List<SensorReading>
        {
            new() { Id = 1, Temperature = 20.0, Timestamp = from.AddHours(1) },
            new() { Id = 2, Temperature = 22.0, Timestamp = from.AddHours(2) }
        };
        _repositoryMock.Setup(r => r.GetReadingsByDateRangeAsync(from, to))
            .ReturnsAsync(expectedReadings);

        // Act
        var result = await _controller.GetHistory(from, to);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedReadings);
    }

    [TestMethod]
    public async Task GetHistory_WithNoDates_UsesDefaultRange()
    {
        // Arrange
        var expectedReadings = new List<SensorReading>();
        _repositoryMock.Setup(r => r.GetReadingsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(expectedReadings);

        // Act
        var result = await _controller.GetHistory(null, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _repositoryMock.Verify(r => r.GetReadingsByDateRangeAsync(
            It.Is<DateTime>(d => d < DateTime.UtcNow.AddDays(-6)),
            It.Is<DateTime>(d => d <= DateTime.UtcNow)), Times.Once);
    }

    [TestMethod]
    public async Task GetStatistics_ReturnsStatisticsObject()
    {
        // Arrange
        var averages = new Dictionary<DateTime, double>
        {
            { DateTime.Today.AddDays(-1), 22.5 },
            { DateTime.Today, 23.0 }
        };
        _repositoryMock.Setup(r => r.GetDailyAveragesAsync("temperature", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(averages);
        _repositoryMock.Setup(r => r.GetTotalReadingsCountAsync())
            .ReturnsAsync(100);

        // Act
        var result = await _controller.GetStatistics("temperature", 7);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }
}
