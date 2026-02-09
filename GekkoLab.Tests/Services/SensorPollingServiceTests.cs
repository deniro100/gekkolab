using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services;
using GekkoLab.Services.Bme280Reader;
using GekkoLab.Services.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class SensorPollingServiceTests
{
    private Mock<ILogger<SensorPollingService>> _loggerMock = null!;
    private Mock<IBme280Reader> _sensorReaderMock = null!;
    private Mock<ISensorReadingRepository> _repositoryMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<IServiceScope> _scopeMock = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SensorPollingService>>();
        _sensorReaderMock = new Mock<IBme280Reader>();
        _repositoryMock = new Mock<ISensorReadingRepository>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        // Setup service scope factory chain
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ISensorReadingRepository)))
            .Returns(_repositoryMock.Object);

        _scopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);
    }

    private IConfiguration CreateConfiguration(string pollingInterval = "00:00:05")
    {
        var configData = new Dictionary<string, string?>
        {
            { "SensorConfiguration:PollingInterval", pollingInterval }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [TestMethod]
    public async Task StartAsync_WithValidSensorData_SavesReading()
    {
        // Arrange
        var config = CreateConfiguration("00:00:01");
        var sensorData = new Bme280Data(
            TemperatureCelsius: 25.5,
            Humidity: 60.0,
            MillimetersOfMercury: 760.0,
            Timestamp: DateTime.UtcNow,
            Metadata: new Bme280DataMetadata("simulator")
        );

        _sensorReaderMock
            .Setup(r => r.ReadSensorDataAsync())
            .Returns(Task.FromResult<Bme280Data?>(sensorData));

        var service = new SensorPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _sensorReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveReadingAsync(It.Is<SensorReading>(
            reading => reading.Temperature == 25.5 &&
                       reading.Humidity == 60.0 &&
                       reading.Pressure == 760.0
        )), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task StartAsync_WithNullSensorTask_DoesNotSaveReading()
    {
        // Arrange
        var config = CreateConfiguration("00:00:01");

        _sensorReaderMock
            .Setup(r => r.ReadSensorDataAsync())
            .ReturnsAsync((Bme280Data?)null);

        var service = new SensorPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _sensorReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveReadingAsync(It.IsAny<SensorReading>()), Times.Never);
    }

    [TestMethod]
    public async Task StartAsync_WithNullSensorData_DoesNotSaveReading()
    {
        // Arrange
        var config = CreateConfiguration("00:00:01");

        _sensorReaderMock
            .Setup(r => r.ReadSensorDataAsync())
            .Returns(Task.FromResult<Bme280Data?>(null!));

        var service = new SensorPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _sensorReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveReadingAsync(It.IsAny<SensorReading>()), Times.Never);
    }

    [TestMethod]
    public async Task StartAsync_WhenReaderThrows_ContinuesPolling()
    {
        // Arrange
        var config = CreateConfiguration("00:00:01");
        var callCount = 0;
        var sensorData = new Bme280Data(
            TemperatureCelsius: 25.5,
            Humidity: 60.0,
            MillimetersOfMercury: 760.0,
            Timestamp: DateTime.UtcNow,
            Metadata: new Bme280DataMetadata("simulator")
        );

        _sensorReaderMock
            .Setup(r => r.ReadSensorDataAsync())
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new Exception("Test exception");
                }
                return Task.FromResult<Bme280Data?>(sensorData);
            });

        var service = new SensorPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _sensorReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(4));
        await service.StopAsync(CancellationToken.None);

        // Assert - should have retried after first failure
        callCount.Should().BeGreaterThan(1);
    }

    [TestMethod]
    public void Constructor_SetsDefaultPollingInterval_WhenNotConfigured()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act - should not throw
        var service = new SensorPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _sensorReaderMock.Object,
            emptyConfig);

        // Assert
        service.Should().NotBeNull();
    }

    [TestMethod]
    public async Task StartAsync_SetsCorrectMetadata()
    {
        // Arrange
        var config = CreateConfiguration("00:00:01");
        var sensorData = new Bme280Data(
            TemperatureCelsius: 25.5,
            Humidity: 60.0,
            MillimetersOfMercury: 760.0,
            Timestamp: DateTime.UtcNow,
            Metadata: new Bme280DataMetadata("bme280")
        );

        _sensorReaderMock
            .Setup(r => r.ReadSensorDataAsync())
            .Returns(Task.FromResult<Bme280Data?>(sensorData));

        var service = new SensorPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _sensorReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveReadingAsync(It.Is<SensorReading>(
            reading => reading.Metadata != null && reading.Metadata.ReaderType == "bme280"
        )), Times.AtLeastOnce);
    }
}
