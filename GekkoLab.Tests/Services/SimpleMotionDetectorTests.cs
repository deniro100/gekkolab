using FluentAssertions;
using GekkoLab.Services.Camera;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class SimpleMotionDetectorTests
{
    private Mock<ILogger<SimpleMotionDetector>> _loggerMock = null!;
    private SimpleMotionDetector _detector = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SimpleMotionDetector>>();
        _detector = new SimpleMotionDetector(_loggerMock.Object);
    }

    [TestMethod]
    public void Sensitivity_DefaultValue_ShouldBe5Percent()
    {
        // Act & Assert
        _detector.Sensitivity.Should().Be(0.05);
    }

    [TestMethod]
    public void Sensitivity_CanBeSet()
    {
        // Arrange
        var newSensitivity = 0.10;

        // Act
        _detector.Sensitivity = newSensitivity;

        // Assert
        _detector.Sensitivity.Should().Be(newSensitivity);
    }

    [TestMethod]
    public void DetectMotion_WithNullPreviousFrame_ShouldReturnFalse()
    {
        // Arrange
        byte[]? previousFrame = null;
        var currentFrame = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = _detector.DetectMotion(previousFrame!, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithNullCurrentFrame_ShouldReturnFalse()
    {
        // Arrange
        var previousFrame = new byte[] { 1, 2, 3, 4, 5 };
        byte[]? currentFrame = null;

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame!);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithEmptyFrames_ShouldReturnFalse()
    {
        // Arrange
        var previousFrame = Array.Empty<byte>();
        var currentFrame = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithIdenticalFrames_ShouldReturnFalse()
    {
        // Arrange
        var frame = new byte[1000];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = (byte)(i % 256);
        }

        var previousFrame = (byte[])frame.Clone();
        var currentFrame = (byte[])frame.Clone();

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithSignificantChanges_ShouldReturnTrue()
    {
        // Arrange
        var previousFrame = new byte[1000];
        var currentFrame = new byte[1000];

        // Fill with different values to create significant difference
        for (int i = 0; i < 1000; i++)
        {
            previousFrame[i] = 0;
            currentFrame[i] = 255; // Maximum difference
        }

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void DetectMotion_WithSmallChanges_ShouldReturnFalse()
    {
        // Arrange
        var previousFrame = new byte[1000];
        var currentFrame = new byte[1000];

        // Fill with similar values (small difference)
        for (int i = 0; i < 1000; i++)
        {
            previousFrame[i] = 100;
            currentFrame[i] = 102; // Very small difference
        }

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithHighSensitivity_ShouldDetectSmallChanges()
    {
        // Arrange
        _detector.Sensitivity = 0.005; // Very sensitive (0.5%)

        var previousFrame = new byte[1000];
        var currentFrame = new byte[1000];

        for (int i = 0; i < 1000; i++)
        {
            previousFrame[i] = 100;
            currentFrame[i] = 105; // Small but detectable difference
        }

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void DetectMotion_WithLowSensitivity_ShouldIgnoreMediumChanges()
    {
        // Arrange
        _detector.Sensitivity = 0.5; // Very low sensitivity (50%)

        var previousFrame = new byte[1000];
        var currentFrame = new byte[1000];

        for (int i = 0; i < 1000; i++)
        {
            previousFrame[i] = 50;
            currentFrame[i] = 100; // Moderate difference
        }

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }
}
