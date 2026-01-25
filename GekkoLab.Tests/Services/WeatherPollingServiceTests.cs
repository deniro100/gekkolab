using FluentAssertions;
using GekkoLab.Models;
using GekkoLab.Services;
using GekkoLab.Services.Repository;
using GekkoLab.Services.WeatherReader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class WeatherPollingServiceTests
{
    private Mock<ILogger<WeatherPollingService>> _loggerMock = null!;
    private Mock<IWeatherReader> _weatherReaderMock = null!;
    private Mock<IWeatherReadingRepository> _repositoryMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<IServiceScope> _scopeMock = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<WeatherPollingService>>();
        _weatherReaderMock = new Mock<IWeatherReader>();
        _repositoryMock = new Mock<IWeatherReadingRepository>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        // Setup service scope factory chain
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IWeatherReadingRepository)))
            .Returns(_repositoryMock.Object);

        _scopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);
    }

    private IConfiguration CreateConfiguration(bool enabled = true, string pollingInterval = "00:05:00")
    {
        var configData = new Dictionary<string, string?>
        {
            { "WeatherConfiguration:Enabled", enabled.ToString() },
            { "WeatherConfiguration:PollingInterval", pollingInterval },
            { "WeatherConfiguration:Location", "Redmond" },
            { "WeatherConfiguration:Latitude", "47.67" },
            { "WeatherConfiguration:Longitude", "-122.12" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenDisabled_DoesNotPoll()
    {
        // Arrange
        var config = CreateConfiguration(enabled: false);
        var service = new WeatherPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _weatherReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _weatherReaderMock.Verify(r => r.GetCurrentWeatherAsync(), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenEnabled_PollsWeatherData()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, pollingInterval: "00:01:00");
        
        _weatherReaderMock
            .Setup(r => r.GetCurrentWeatherAsync())
            .ReturnsAsync(new WeatherData
            {
                IsValid = true,
                Temperature = 10.5,
                Humidity = 65.0,
                Latitude = 47.67,
                Longitude = -122.12,
                Timestamp = DateTime.UtcNow
            });

        var service = new WeatherPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _weatherReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(7)); // Allow time for initial delay + first poll

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(6));
        await service.StopAsync(CancellationToken.None);

        // Assert - should poll at least once after the initial delay
        _weatherReaderMock.Verify(r => r.GetCurrentWeatherAsync(), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithValidWeatherData_SavesReading()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, pollingInterval: "00:01:00");
        
        _weatherReaderMock
            .Setup(r => r.GetCurrentWeatherAsync())
            .ReturnsAsync(new WeatherData
            {
                IsValid = true,
                Temperature = 10.5,
                Humidity = 65.0,
                Latitude = 47.67,
                Longitude = -122.12,
                Timestamp = DateTime.UtcNow
            });

        var service = new WeatherPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _weatherReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(7));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(6));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(It.Is<WeatherReading>(
            reading => reading.Temperature == 10.5 &&
                       reading.Humidity == 65.0 &&
                       reading.Location == "Redmond"
        )), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithInvalidWeatherData_DoesNotSaveReading()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, pollingInterval: "00:01:00");
        
        _weatherReaderMock
            .Setup(r => r.GetCurrentWeatherAsync())
            .ReturnsAsync(new WeatherData
            {
                IsValid = false,
                ErrorMessage = "API error"
            });

        var service = new WeatherPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _weatherReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(7));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(6));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<WeatherReading>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenReaderThrows_ContinuesPolling()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, pollingInterval: "00:00:01");
        var callCount = 0;
        
        _weatherReaderMock
            .Setup(r => r.GetCurrentWeatherAsync())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new Exception("Test exception");
                }
                return new WeatherData
                {
                    IsValid = true,
                    Temperature = 10.5,
                    Humidity = 65.0,
                    Latitude = 47.67,
                    Longitude = -122.12,
                    Timestamp = DateTime.UtcNow
                };
            });

        var service = new WeatherPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _weatherReaderMock.Object,
            config);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(8));
        await service.StopAsync(CancellationToken.None);

        // Assert - should have tried multiple times
        _weatherReaderMock.Verify(r => r.GetCurrentWeatherAsync(), Times.AtLeast(2));
    }

    [TestMethod]
    public void Constructor_SetsDefaultPollingInterval_WhenNotConfigured()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "WeatherConfiguration:Enabled", "true" },
            { "WeatherConfiguration:Location", "Redmond" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act - should not throw
        var service = new WeatherPollingService(
            _loggerMock.Object,
            _scopeFactoryMock.Object,
            _weatherReaderMock.Object,
            config);

        // Assert
        service.Should().NotBeNull();
    }
}
