namespace GekkoLab.Services.Camera;

/// <summary>
/// Background service that monitors the camera for motion and saves images
/// Images are stored locally when motion is detected, with a minimum interval between captures
/// </summary>
public class MotionDetectionService : BackgroundService
{
    private readonly ILogger<MotionDetectionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ICameraCapture _camera;
    private readonly IMotionDetector _motionDetector;
    
    private byte[]? _previousFrame;
    private DateTime _lastCaptureTime = DateTime.MinValue;
    private string _captureDirectory = null!;

    public MotionDetectionService(
        ILogger<MotionDetectionService> logger,
        IConfiguration configuration,
        ICameraCaptureProvider cameraCaptureProvider,
        IMotionDetector motionDetector)
    {
        _logger = logger;
        _configuration = configuration;
        _camera = cameraCaptureProvider.GetCapture();
        _motionDetector = motionDetector;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("CameraConfiguration:MotionDetection:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("Motion detection is disabled via configuration");
            return;
        }

        // Get configuration
        var pollingInterval = _configuration.GetValue<TimeSpan>("CameraConfiguration:MotionDetection:PollingInterval", TimeSpan.FromSeconds(1));
        var minCaptureInterval = _configuration.GetValue<TimeSpan>("CameraConfiguration:MotionDetection:MinCaptureInterval", TimeSpan.FromSeconds(5));
        var sensitivity = _configuration.GetValue<double>("CameraConfiguration:MotionDetection:Sensitivity", 0.05);
        _captureDirectory = _configuration.GetValue<string>("CameraConfiguration:MotionDetection:CaptureDirectory", "gekkodata/motion-captures")!;

        // Apply sensitivity
        _motionDetector.Sensitivity = sensitivity;

        // Ensure capture directory exists
        Directory.CreateDirectory(_captureDirectory);

        _logger.LogInformation(
            "Motion detection service started. Polling: {Polling}, Min capture interval: {MinCapture}, Sensitivity: {Sensitivity:P0}, Directory: {Directory}",
            pollingInterval, minCaptureInterval, sensitivity, _captureDirectory);

        if (!_camera.IsAvailable)
        {
            _logger.LogWarning("Camera is not available. Motion detection will not work.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessFrameAsync(minCaptureInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in motion detection loop");
            }

            await Task.Delay(pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessFrameAsync(TimeSpan minCaptureInterval)
    {
        var currentFrame = await _camera.CaptureFrameAsync();
        if (currentFrame == null)
        {
            _logger.LogWarning("Failed to capture frame");
            return;
        }

        // Check for motion if we have a previous frame
        if (_previousFrame != null)
        {
            var motionDetected = _motionDetector.DetectMotion(_previousFrame, currentFrame);

            if (motionDetected)
            {
                var timeSinceLastCapture = DateTime.UtcNow - _lastCaptureTime;

                if (timeSinceLastCapture >= minCaptureInterval)
                {
                    await SaveCaptureAsync(currentFrame);
                    _lastCaptureTime = DateTime.UtcNow;
                }
                else
                {
                    _logger.LogDebug(
                        "Motion detected but skipping capture (last capture was {Seconds:F1}s ago, minimum interval is {MinInterval}s)",
                        timeSinceLastCapture.TotalSeconds, minCaptureInterval.TotalSeconds);
                }
            }
        }

        _previousFrame = currentFrame;
    }

    private async Task SaveCaptureAsync(byte[] imageData)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            var filename = $"motion_{timestamp:yyyyMMdd_HHmmss_fff}.jpg";
            var filepath = Path.Combine(_captureDirectory, filename);

            await File.WriteAllBytesAsync(filepath, imageData);

            _logger.LogInformation("Motion captured: {Filename} ({Size} bytes)", filename, imageData.Length);

            // Clean up old files if configured
            await CleanupOldCapturesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save motion capture");
        }
    }

    private Task CleanupOldCapturesAsync()
    {
        try
        {
            var maxAgeDays = _configuration.GetValue<int>("CameraConfiguration:MotionDetection:MaxCaptureAgeDays", 7);
            var maxFiles = _configuration.GetValue<int>("CameraConfiguration:MotionDetection:MaxCaptureFiles", 1000);

            var directory = new DirectoryInfo(_captureDirectory);
            if (!directory.Exists) return Task.CompletedTask;

            var files = directory.GetFiles("motion_*.jpg")
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            // Delete files older than max age
            var cutoffDate = DateTime.UtcNow.AddDays(-maxAgeDays);
            foreach (var file in files.Where(f => f.CreationTimeUtc < cutoffDate))
            {
                try
                {
                    file.Delete();
                    _logger.LogDebug("Deleted old capture: {Filename}", file.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old capture: {Filename}", file.Name);
                }
            }

            // Delete excess files (keep only maxFiles)
            var remainingFiles = directory.GetFiles("motion_*.jpg")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(maxFiles)
                .ToList();

            foreach (var file in remainingFiles)
            {
                try
                {
                    file.Delete();
                    _logger.LogDebug("Deleted excess capture: {Filename}", file.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete excess capture: {Filename}", file.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during capture cleanup");
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _camera.Dispose();
        base.Dispose();
    }
}
