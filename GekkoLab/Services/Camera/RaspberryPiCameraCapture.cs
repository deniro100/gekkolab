using System.Diagnostics;

namespace GekkoLab.Services.Camera;

/// <summary>
/// Camera capture implementation for Raspberry Pi using rpicam-still
/// Works with Raspberry Pi Camera Module v2 and v3 on Raspberry Pi OS Bookworm
/// </summary>
public class RaspberryPiCameraCapture : ICameraCapture
{
    private const string CameraCommand = "rpicam-still";
    
    private readonly ILogger<RaspberryPiCameraCapture> _logger;
    private readonly int _width;
    private readonly int _height;
    private readonly int _quality;
    private bool _isAvailable;
    private bool _disposed;

    public RaspberryPiCameraCapture(
        ILogger<RaspberryPiCameraCapture> logger,
        int width = 1280,
        int height = 720,
        int quality = 85)
    {
        _logger = logger;
        _width = width;
        _height = height;
        _quality = quality;
        _isAvailable = CheckCameraAvailability();
    }

    public bool IsAvailable => _isAvailable;

    public async Task<byte[]?> CaptureFrameAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Attempted to capture frame after disposal");
            return null;
        }

        if (!_isAvailable)
        {
            _logger.LogWarning("Camera is not available");
            return null;
        }

        try
        {
            // Use rpicam-still to capture a frame
            var tempFile = Path.Combine(Path.GetTempPath(), $"capture_{Guid.NewGuid()}.jpg");

            var processInfo = new ProcessStartInfo
            {
                FileName = CameraCommand,
                Arguments = $"-o {tempFile} --width {_width} --height {_height} --quality {_quality} --nopreview --immediate -t 1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start {Command} process", CameraCommand);
                return null;
            }

            var timeout = TimeSpan.FromSeconds(10);
            var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

            if (!completed)
            {
                _logger.LogError("Camera capture timed out");
                try { process.Kill(); } catch { }
                return null;
            }

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("Camera capture failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                return null;
            }

            if (!File.Exists(tempFile))
            {
                _logger.LogError("Capture file was not created");
                return null;
            }

            var imageData = await File.ReadAllBytesAsync(tempFile);
            
            // Clean up temp file
            try { File.Delete(tempFile); } catch { }

            _logger.LogDebug("Captured frame: {Size} bytes", imageData.Length);
            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing frame from camera");
            return null;
        }
    }

    private bool CheckCameraAvailability()
    {
        _logger.LogInformation("Checking camera availability for command: {Command}", CameraCommand);
        
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = CameraCommand,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogWarning("Failed to start 'which' process");
                return false;
            }

            process.WaitForExit(TimeSpan.FromSeconds(5));
            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();

            _logger.LogInformation("'which {Command}' exit code: {ExitCode}, output: '{Output}', error: '{Error}'", 
                CameraCommand, process.ExitCode, output, error);

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogWarning("{Command} not found. Camera support unavailable. Make sure rpicam-apps is installed.", CameraCommand);
                return false;
            }

            _logger.LogInformation("Camera support available via {Command} at {Path}", CameraCommand, output);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not check camera availability");
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _isAvailable = false;
        }
    }
}
