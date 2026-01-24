namespace GekkoLab.Services.Camera;

/// <summary>
/// Simulator camera capture for development and testing
/// Generates random "frames" with occasional simulated motion
/// </summary>
public class SimulatorCameraCapture : ICameraCapture
{
    private readonly ILogger<SimulatorCameraCapture> _logger;
    private readonly Random _random = new();
    private bool _disposed;
    private int _frameCounter;

    public SimulatorCameraCapture(ILogger<SimulatorCameraCapture> logger)
    {
        _logger = logger;
        _logger.LogInformation("Simulator camera initialized");
    }

    public bool IsAvailable => !_disposed;

    public Task<byte[]?> CaptureFrameAsync()
    {
        if (_disposed)
        {
            return Task.FromResult<byte[]?>(null);
        }

        _frameCounter++;

        // Generate a simulated JPEG-like byte array
        // In real usage, this would be actual image data
        // We simulate by creating a byte array with some variation
        var frameSize = 50000 + _random.Next(10000); // ~50-60KB simulated frame
        var frame = new byte[frameSize];
        _random.NextBytes(frame);

        // Add JPEG magic bytes for realism (FFD8 start, FFD9 end)
        frame[0] = 0xFF;
        frame[1] = 0xD8;
        frame[frameSize - 2] = 0xFF;
        frame[frameSize - 1] = 0xD9;

        // Inject frame number for motion detection simulation
        // Every 3-7 frames, make a significant change (simulated motion)
        var motionInterval = 3 + (_frameCounter % 5);
        if (_frameCounter % motionInterval == 0)
        {
            // Simulate motion by making more dramatic changes
            for (int i = 100; i < Math.Min(5000, frameSize); i += 10)
            {
                frame[i] = (byte)(_random.Next(256));
            }
            _logger.LogDebug("Simulator: Generated frame {FrameNumber} with simulated motion", _frameCounter);
        }
        else
        {
            _logger.LogDebug("Simulator: Generated frame {FrameNumber} (static)", _frameCounter);
        }

        return Task.FromResult<byte[]?>(frame);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogInformation("Simulator camera disposed");
        }
    }
}
