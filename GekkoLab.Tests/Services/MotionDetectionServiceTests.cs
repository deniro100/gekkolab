using FluentAssertions;
using GekkoLab.Services.Camera;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class MotionDetectionServiceTests
{
    private Mock<ILogger<MotionDetectionService>> _loggerMock = null!;
    private Mock<ICameraCapture> _cameraMock = null!;
    private Mock<ICameraCaptureProvider> _cameraProviderMock = null!;
    private Mock<IMotionDetector> _motionDetectorMock = null!;
    private string _captureDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MotionDetectionService>>();
        _cameraMock = new Mock<ICameraCapture>();
        _cameraProviderMock = new Mock<ICameraCaptureProvider>();
        _motionDetectorMock = new Mock<IMotionDetector>();

        _cameraProviderMock
            .Setup(p => p.GetCapture())
            .Returns(_cameraMock.Object);

        _captureDirectory = Path.Combine(Path.GetTempPath(), "motion-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_captureDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_captureDirectory))
        {
            Directory.Delete(_captureDirectory, recursive: true);
        }
    }

    private IConfiguration CreateConfiguration(bool enabled = true, bool useSimulator = true, string pollingInterval = "00:00:01")
    {
        var configData = new Dictionary<string, string?>
        {
            { "CameraConfiguration:MotionDetection:Enabled", enabled.ToString() },
            { "CameraConfiguration:UseSimulator", useSimulator.ToString() },
            { "CameraConfiguration:MotionDetection:PollingInterval", pollingInterval },
            { "CameraConfiguration:MotionDetection:MinCaptureInterval", "00:00:01" },
            { "CameraConfiguration:MotionDetection:Sensitivity", "0.05" },
            { "CameraConfiguration:MotionDetection:CaptureDirectory", _captureDirectory },
            { "CameraConfiguration:MotionDetection:MaxCaptureAgeDays", "7" },
            { "CameraConfiguration:MotionDetection:MaxCaptureFiles", "100" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenDisabled_DoesNotCapture()
    {
        // Arrange
        var config = CreateConfiguration(enabled: false);
        _cameraMock.Setup(c => c.IsAvailable).Returns(true);

        var service = new MotionDetectionService(
            _loggerMock.Object,
            config,
            _cameraProviderMock.Object,
            _motionDetectorMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _cameraMock.Verify(c => c.CaptureFrameAsync(), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenCameraNotAvailable_DoesNotCapture()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true);
        _cameraMock.Setup(c => c.IsAvailable).Returns(false);

        var service = new MotionDetectionService(
            _loggerMock.Object,
            config,
            _cameraProviderMock.Object,
            _motionDetectorMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _cameraMock.Verify(c => c.CaptureFrameAsync(), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenEnabled_CapturesFrames()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true);
        _cameraMock.Setup(c => c.IsAvailable).Returns(true);
        _cameraMock.Setup(c => c.CaptureFrameAsync()).ReturnsAsync(new byte[] { 1, 2, 3 });
        _motionDetectorMock.Setup(m => m.DetectMotion(It.IsAny<byte[]>(), It.IsAny<byte[]>())).Returns(false);

        var service = new MotionDetectionService(
            _loggerMock.Object,
            config,
            _cameraProviderMock.Object,
            _motionDetectorMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        _cameraMock.Verify(c => c.CaptureFrameAsync(), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenMotionDetected_SavesCapture()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, pollingInterval: "00:00:01");
        _cameraMock.Setup(c => c.IsAvailable).Returns(true);
        _cameraMock.Setup(c => c.CaptureFrameAsync()).ReturnsAsync(new byte[] { 1, 2, 3 });
        _motionDetectorMock.Setup(m => m.DetectMotion(It.IsAny<byte[]>(), It.IsAny<byte[]>())).Returns(true);

        var service = new MotionDetectionService(
            _loggerMock.Object,
            config,
            _cameraProviderMock.Object,
            _motionDetectorMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(4));
        await service.StopAsync(CancellationToken.None);

        // Assert - check that files were saved
        var files = Directory.GetFiles(_captureDirectory, "motion_*.jpg");
        files.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_SetsSensitivityOnMotionDetector()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true);
        _cameraMock.Setup(c => c.IsAvailable).Returns(true);

        var service = new MotionDetectionService(
            _loggerMock.Object,
            config,
            _cameraProviderMock.Object,
            _motionDetectorMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _motionDetectorMock.VerifySet(m => m.Sensitivity = 0.05);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var newDirectory = Path.Combine(Path.GetTempPath(), "new-motion-test-" + Guid.NewGuid());
        var configData = new Dictionary<string, string?>
        {
            { "CameraConfiguration:MotionDetection:Enabled", "true" },
            { "CameraConfiguration:MotionDetection:CaptureDirectory", newDirectory }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _cameraMock.Setup(c => c.IsAvailable).Returns(true);

        var service = new MotionDetectionService(
            _loggerMock.Object,
            config,
            _cameraProviderMock.Object,
            _motionDetectorMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));

        try
        {
            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(200);
            await service.StopAsync(CancellationToken.None);

            // Assert
            Directory.Exists(newDirectory).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(newDirectory))
            {
                Directory.Delete(newDirectory, recursive: true);
            }
        }
    }
}
