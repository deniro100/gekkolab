using FluentAssertions;
using GekkoLab.Services.Camera;
using Microsoft.Extensions.Logging;
using Moq;

namespace GekkoLab.Tests.Services;

[TestClass]
public class SimulatorCameraCaptureTests
{
    private Mock<ILogger<SimulatorCameraCapture>> _loggerMock = null!;
    private SimulatorCameraCapture _camera = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SimulatorCameraCapture>>();
        _camera = new SimulatorCameraCapture(_loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _camera?.Dispose();
    }

    [TestMethod]
    public void IsAvailable_WhenNotDisposed_ShouldReturnTrue()
    {
        // Act & Assert
        _camera.IsAvailable.Should().BeTrue();
    }

    [TestMethod]
    public void IsAvailable_WhenDisposed_ShouldReturnFalse()
    {
        // Arrange
        _camera.Dispose();

        // Act & Assert
        _camera.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public async Task CaptureFrameAsync_WhenNotDisposed_ShouldReturnImageData()
    {
        // Act
        var result = await _camera.CaptureFrameAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Length.Should().BeGreaterThan(0);
        
        // Check JPEG magic bytes
        result[0].Should().Be(0xFF);
        result[1].Should().Be(0xD8);
        result[^2].Should().Be(0xFF);
        result[^1].Should().Be(0xD9);
    }

    [TestMethod]
    public async Task CaptureFrameAsync_WhenDisposed_ShouldReturnNull()
    {
        // Arrange
        _camera.Dispose();

        // Act
        var result = await _camera.CaptureFrameAsync();

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task CaptureFrameAsync_MultipleCalls_ShouldReturnDifferentFrames()
    {
        // Act
        var frame1 = await _camera.CaptureFrameAsync();
        var frame2 = await _camera.CaptureFrameAsync();

        // Assert
        frame1.Should().NotBeNull();
        frame2.Should().NotBeNull();
        
        // Frames should be different (randomly generated)
        frame1.Should().NotEqual(frame2);
    }
}
