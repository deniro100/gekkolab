using FluentAssertions;
using GekkoLab.Services.GekkoDetector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class GekkoDetectorProviderTests
{
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    private IConfiguration CreateConfiguration(bool useSimulator = true, string modelPath = "models/gekko_detector.onnx")
    {
        var configData = new Dictionary<string, string?>
        {
            { "GekkoDetector:UseSimulator", useSimulator.ToString() },
            { "GekkoDetector:ModelPath", modelPath }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [TestMethod]
    public void GetDetector_WhenSimulatorEnabled_ReturnsSimulatorDetector()
    {
        // Arrange
        var config = CreateConfiguration(useSimulator: true);
        var provider = new GekkoDetectorProvider(config, _loggerFactoryMock.Object);

        // Act
        var detector = provider.GetDetector();

        // Assert
        detector.Should().BeOfType<SimulatorGekkoDetector>();
    }

    [TestMethod]
    public void GetDetector_WhenModelNotFound_ReturnsSimulatorDetector()
    {
        // Arrange
        var config = CreateConfiguration(useSimulator: false, modelPath: "nonexistent/model.onnx");
        var provider = new GekkoDetectorProvider(config, _loggerFactoryMock.Object);

        // Act
        var detector = provider.GetDetector();

        // Assert
        detector.Should().BeOfType<SimulatorGekkoDetector>();
    }

    [TestMethod]
    public void GetDetector_ReturnsSameInstance_OnMultipleCalls()
    {
        // Arrange
        var config = CreateConfiguration(useSimulator: true);
        var provider = new GekkoDetectorProvider(config, _loggerFactoryMock.Object);

        // Act
        var detector1 = provider.GetDetector();
        var detector2 = provider.GetDetector();

        // Assert
        detector1.Should().BeSameAs(detector2);
    }

    [TestMethod]
    public void GetDetector_SimulatorDetector_IsModelLoadedReturnsTrue()
    {
        // Arrange
        var config = CreateConfiguration(useSimulator: true);
        var provider = new GekkoDetectorProvider(config, _loggerFactoryMock.Object);

        // Act
        var detector = provider.GetDetector();

        // Assert
        detector.IsModelLoaded.Should().BeTrue();
    }
}

[TestClass]
public class SimulatorGekkoDetectorTests
{
    private Mock<ILogger<SimulatorGekkoDetector>> _loggerMock = null!;
    private SimulatorGekkoDetector _detector = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SimulatorGekkoDetector>>();
        _detector = new SimulatorGekkoDetector(_loggerMock.Object);
    }

    [TestMethod]
    public void IsModelLoaded_ReturnsTrue()
    {
        // Assert
        _detector.IsModelLoaded.Should().BeTrue();
    }

    [TestMethod]
    public async Task DetectAsync_ReturnsDetectionResult()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3, 4, 5 };
        var imagePath = "/path/to/image.jpg";

        // Act
        var result = await _detector.DetectAsync(imageData, imagePath);

        // Assert
        result.Should().NotBeNull();
        result.ImagePath.Should().Be(imagePath);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.Confidence.Should().BeInRange(0, 1);
        result.Label.Should().BeOneOf("gecko", "no_gecko");
    }

    [TestMethod]
    public async Task DetectAsync_SetsGekkoDetectedBasedOnConfidence()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3, 4, 5 };
        var imagePath = "/path/to/image.jpg";

        // Act - run multiple times to get both outcomes
        var results = new List<GekkoLab.Models.GekkoDetectionResult>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(await _detector.DetectAsync(imageData, imagePath));
        }

        // Assert
        // With 30% chance of detection, we should have some detections and some non-detections
        results.Any(r => r.GekkoDetected).Should().BeTrue("should have some detections");
        results.Any(r => !r.GekkoDetected).Should().BeTrue("should have some non-detections");
    }

    [TestMethod]
    public async Task DetectAsync_LabelMatchesGekkoDetected()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3, 4, 5 };
        var imagePath = "/path/to/image.jpg";

        // Act
        for (int i = 0; i < 50; i++)
        {
            var result = await _detector.DetectAsync(imageData, imagePath);

            // Assert
            if (result.GekkoDetected)
            {
                result.Label.Should().Be("gecko");
            }
            else
            {
                result.Label.Should().Be("no_gecko");
            }
        }
    }

    [TestMethod]
    public void Dispose_DoesNotThrow()
    {
        // Act & Assert
        _detector.Invoking(d => d.Dispose()).Should().NotThrow();
    }
}
