using FluentAssertions;
using GekkoLab.Services.Bme280Reader;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class Bme280SimulatorReaderTests
{
    private Mock<ILogger<Bme280SimulatorReader>> _loggerMock = null!;
    private Bme280SimulatorReader _reader = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<Bme280SimulatorReader>>();
        _reader = new Bme280SimulatorReader(_loggerMock.Object);
    }

    [TestMethod]
    public async Task ReadSensorDataAsync_ReturnsValidData()
    {
        // Act
        var result = await _reader.ReadSensorDataAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [TestMethod]
    public async Task ReadSensorDataAsync_TemperatureIsInExpectedRange()
    {
        // Act
        var result = await _reader.ReadSensorDataAsync();

        // Assert
        result.TemperatureCelsius.Should().BeInRange(20.0, 30.0);
    }

    [TestMethod]
    public async Task ReadSensorDataAsync_HumidityIsInExpectedRange()
    {
        // Act
        var result = await _reader.ReadSensorDataAsync();

        // Assert
        result.Humidity.Should().BeInRange(40.0, 70.0);
    }

    [TestMethod]
    public async Task ReadSensorDataAsync_PressureIsInExpectedRange()
    {
        // Act
        var result = await _reader.ReadSensorDataAsync();

        // Assert
        result.MillimetersOfMercury.Should().BeInRange(740.0, 780.0);
    }

    [TestMethod]
    public async Task ReadSensorDataAsync_TimestampIsRecent()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var result = await _reader.ReadSensorDataAsync();

        // Assert
        var after = DateTime.UtcNow;
        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }

    [TestMethod]
    public void IsAvailable_ReturnsTrue()
    {
        // Act & Assert
        _reader.IsAvailable.Should().BeTrue();
    }

    [TestMethod]
    public async Task ReadSensorDataAsync_MetadataReaderTypeIsSimulator()
    {
        // Act
        var result = await _reader.ReadSensorDataAsync();

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.ReaderType.Should().Be("simulator");
    }

    [TestMethod]
    public async Task ReadSensorDataAsync_ReturnsRandomValues()
    {
        // Act
        var result1 = await _reader.ReadSensorDataAsync();
        var result2 = await _reader.ReadSensorDataAsync();

        // Assert - at least one value should differ (statistically very likely)
        var allSame = Math.Abs(result1.TemperatureCelsius - result2.TemperatureCelsius) < 0.0001 &&
                      Math.Abs(result1.Humidity - result2.Humidity) < 0.0001 &&
                      Math.Abs(result1.MillimetersOfMercury - result2.MillimetersOfMercury) < 0.0001;
        allSame.Should().BeFalse("Random values should differ between calls");
    }
}
