using FluentAssertions;
using GekkoLab.Services.Bme280Reader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class Bme280SensorReaderProviderTests
{
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    [TestMethod]
    public void GetReader_WhenUseSimulatorIsTrue_ReturnsSimulatorReader()
    {
        // Arrange
        var configuration = CreateConfiguration(useSimulator: true);
        var provider = new Bme280SensorReaderProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var reader = provider.GetReader();

        // Assert
        reader.Should().BeOfType<Bme280SimulatorReader>();
    }

    [TestMethod]
    public void GetReader_WhenUseSimulatorIsFalse_ReturnsHardwareReader()
    {
        // Arrange
        var configuration = CreateConfiguration(useSimulator: false);
        var provider = new Bme280SensorReaderProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var reader = provider.GetReader();

        // Assert
        reader.Should().BeOfType<Bme280Reader>();
    }

    [TestMethod]
    public void GetReader_WhenConfigurationMissing_DefaultsToHardwareReader()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provider = new Bme280SensorReaderProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var reader = provider.GetReader();

        // Assert
        reader.Should().BeOfType<Bme280Reader>();
    }

    [TestMethod]
    public void GetReader_IsLazyInitialized_ReaderCreatedOnFirstCall()
    {
        // Arrange
        var configuration = CreateConfiguration(useSimulator: true);
        var provider = new Bme280SensorReaderProvider(configuration, _loggerFactoryMock.Object);

        // Logger should not be created during construction (lazy)
        _loggerFactoryMock.Verify(f => f.CreateLogger(It.IsAny<string>()), Times.Never);

        // Act
        _ = provider.GetReader();

        // Assert - logger is created when reader is accessed
        _loggerFactoryMock.Verify(f => f.CreateLogger(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public void GetReader_CalledMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        var configuration = CreateConfiguration(useSimulator: true);
        var provider = new Bme280SensorReaderProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var reader1 = provider.GetReader();
        var reader2 = provider.GetReader();

        // Assert
        reader1.Should().BeSameAs(reader2);
    }

    private static IConfiguration CreateConfiguration(bool useSimulator)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "SensorConfiguration:UseSimulator", useSimulator.ToString() }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }
}
