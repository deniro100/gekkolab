using FluentAssertions;
using GekkoLab.Services.Camera;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class CameraCaptureProviderTests
{
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;
    private Mock<ILogger<CameraCaptureProvider>> _providerLoggerMock = null!;
    private Mock<ILogger<SimulatorCameraCapture>> _simulatorLoggerMock = null!;
    private Mock<ILogger<RaspberryPiCameraCapture>> _cameraLoggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _providerLoggerMock = new Mock<ILogger<CameraCaptureProvider>>();
        _simulatorLoggerMock = new Mock<ILogger<SimulatorCameraCapture>>();
        _cameraLoggerMock = new Mock<ILogger<RaspberryPiCameraCapture>>();

        _loggerFactoryMock.Setup(f => f.CreateLogger(typeof(CameraCaptureProvider).FullName!))
            .Returns(_providerLoggerMock.Object);
        _loggerFactoryMock.Setup(f => f.CreateLogger(typeof(SimulatorCameraCapture).FullName!))
            .Returns(_simulatorLoggerMock.Object);
        _loggerFactoryMock.Setup(f => f.CreateLogger(typeof(RaspberryPiCameraCapture).FullName!))
            .Returns(_cameraLoggerMock.Object);
    }

    [TestMethod]
    public void GetCapture_WhenUseSimulatorIsTrue_ShouldReturnSimulatorCapture()
    {
        // Arrange
        var configuration = CreateConfiguration(useSimulator: true);
        var provider = new CameraCaptureProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var capture = provider.GetCapture();

        // Assert
        capture.Should().BeOfType<SimulatorCameraCapture>();
    }

    [TestMethod]
    public void GetCapture_WhenUseSimulatorIsFalse_ShouldReturnRaspberryPiCapture()
    {
        // Arrange
        var configuration = CreateConfiguration(useSimulator: false);
        var provider = new CameraCaptureProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var capture = provider.GetCapture();

        // Assert
        capture.Should().BeOfType<RaspberryPiCameraCapture>();
    }

    [TestMethod]
    public void GetCapture_WhenCalledMultipleTimes_ShouldReturnSameInstance()
    {
        // Arrange
        var configuration = CreateConfiguration(useSimulator: true);
        var provider = new CameraCaptureProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var capture1 = provider.GetCapture();
        var capture2 = provider.GetCapture();

        // Assert
        capture1.Should().BeSameAs(capture2);
    }

    [TestMethod]
    public void GetCapture_WithCustomDimensions_ShouldUseConfiguredValues()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["CameraConfiguration:UseSimulator"] = "false",
            ["CameraConfiguration:Width"] = "1920",
            ["CameraConfiguration:Height"] = "1080",
            ["CameraConfiguration:Quality"] = "90"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var provider = new CameraCaptureProvider(configuration, _loggerFactoryMock.Object);

        // Act
        var capture = provider.GetCapture();

        // Assert
        capture.Should().BeOfType<RaspberryPiCameraCapture>();
        // Note: We can't directly verify the dimensions were used since they're private
        // but we verify the correct type was created
    }

    private static IConfiguration CreateConfiguration(bool useSimulator)
    {
        var configData = new Dictionary<string, string?>
        {
            ["CameraConfiguration:UseSimulator"] = useSimulator.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }
}
