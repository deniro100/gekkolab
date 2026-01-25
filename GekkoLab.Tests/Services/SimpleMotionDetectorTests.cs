﻿using FluentAssertions;
using GekkoLab.Services.Camera;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
        var currentFrame = CreateSolidColorJpeg(100, 100, 100);

        // Act
        var result = _detector.DetectMotion(previousFrame!, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithNullCurrentFrame_ShouldReturnFalse()
    {
        // Arrange
        var previousFrame = CreateSolidColorJpeg(100, 100, 100);
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
        var currentFrame = CreateSolidColorJpeg(100, 100, 100);

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithIdenticalImages_ShouldReturnFalse()
    {
        // Arrange
        var image = CreateSolidColorJpeg(128, 128, 128);

        // Act
        var result = _detector.DetectMotion(image, image);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithSignificantColorChange_ShouldReturnTrue()
    {
        // Arrange - Black to White is 100% difference
        var previousFrame = CreateSolidColorJpeg(0, 0, 0);      // Black
        var currentFrame = CreateSolidColorJpeg(255, 255, 255); // White

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void DetectMotion_WithSmallColorChange_ShouldReturnFalse()
    {
        // Arrange - Very small color difference (< 5%)
        var previousFrame = CreateSolidColorJpeg(100, 100, 100);
        var currentFrame = CreateSolidColorJpeg(105, 105, 105); // ~2% difference

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithHighSensitivity_ShouldDetectSmallChanges()
    {
        // Arrange
        _detector.Sensitivity = 0.01; // Very sensitive (1%)

        var previousFrame = CreateSolidColorJpeg(100, 100, 100);
        var currentFrame = CreateSolidColorJpeg(110, 110, 110); // ~4% difference

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

        var previousFrame = CreateSolidColorJpeg(50, 50, 50);
        var currentFrame = CreateSolidColorJpeg(150, 150, 150); // ~39% difference

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void DetectMotion_WithPartialChange_ShouldDetectMotion()
    {
        // Arrange - Create an image with half changed
        var previousFrame = CreateSolidColorJpeg(100, 100, 100);
        var currentFrame = CreateHalfChangedJpeg(100, 200); // Half is 100, half is 200

        // Act - Default sensitivity is 5%, half image changed by ~39% = ~20% overall
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public void DetectMotion_WithInvalidJpegData_ShouldReturnFalse()
    {
        // Arrange - Invalid JPEG data
        var previousFrame = new byte[] { 1, 2, 3, 4, 5 };
        var currentFrame = CreateSolidColorJpeg(100, 100, 100);

        // Act
        var result = _detector.DetectMotion(previousFrame, currentFrame);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Creates a JPEG image with solid color
    /// </summary>
    private static byte[] CreateSolidColorJpeg(byte r, byte g, byte b)
    {
        using var image = new Image<Rgb24>(200, 150);
        var color = new Rgb24(r, g, b);
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = color;
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Creates a JPEG image with half one color and half another
    /// </summary>
    private static byte[] CreateHalfChangedJpeg(byte firstHalfGray, byte secondHalfGray)
    {
        using var image = new Image<Rgb24>(200, 150);
        var color1 = new Rgb24(firstHalfGray, firstHalfGray, firstHalfGray);
        var color2 = new Rgb24(secondHalfGray, secondHalfGray, secondHalfGray);
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = x < image.Width / 2 ? color1 : color2;
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }
}
