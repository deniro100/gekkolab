using GekkoLab.Services.Repository;

namespace GekkoLab.Services.GekkoDetector;

/// <summary>
/// Background service that monitors motion capture directory and runs gecko detection
/// on new images using ONNX model
/// </summary>
public class GekkoDetectorService : BackgroundService
{
    private readonly ILogger<GekkoDetectorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGekkoDetector _detector;
    private readonly string _captureDirectory;
    private readonly TimeSpan _pollingInterval;
    private readonly HashSet<string> _processedFiles = new();
    private DateTime _lastProcessedTime = DateTime.MinValue;

    public GekkoDetectorService(
        ILogger<GekkoDetectorService> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IGekkoDetectorProvider detectorProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _detector = detectorProvider.GetDetector();
        
        _captureDirectory = _configuration.GetValue<string>("CameraConfiguration:MotionDetection:CaptureDirectory", "gekkodata/motion-captures")!;
        _pollingInterval = _configuration.GetValue<TimeSpan>("GekkoDetector:PollingInterval", TimeSpan.FromSeconds(10));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("GekkoDetector:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("Gecko detector service is disabled via configuration");
            return;
        }

        _logger.LogInformation("Gecko detector service started. Polling interval: {Interval}, Directory: {Directory}",
            _pollingInterval, _captureDirectory);

        // Initial delay
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNewImagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gecko detector loop");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessNewImagesAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_captureDirectory))
        {
            _logger.LogDebug("Capture directory does not exist: {Directory}", _captureDirectory);
            return;
        }

        var files = new DirectoryInfo(_captureDirectory)
            .GetFiles("motion_*.jpg")
            .Where(f => f.LastWriteTimeUtc > _lastProcessedTime)
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        if (files.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Found {Count} new images to process", files.Count);

        foreach (var file in files)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            if (_processedFiles.Contains(file.FullName))
                continue;

            try
            {
                await ProcessImageAsync(file);
                _processedFiles.Add(file.FullName);
                _lastProcessedTime = file.LastWriteTimeUtc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image: {File}", file.Name);
            }
        }

        // Clean up old entries from processed files set (keep only last 1000)
        if (_processedFiles.Count > 1000)
        {
            var oldFiles = _processedFiles.Take(_processedFiles.Count - 500).ToList();
            foreach (var file in oldFiles)
            {
                _processedFiles.Remove(file);
            }
        }
    }

    private async Task ProcessImageAsync(FileInfo file)
    {
        _logger.LogDebug("Processing image: {File}", file.Name);

        var imageData = await File.ReadAllBytesAsync(file.FullName);
        var result = await _detector.DetectAsync(imageData, file.FullName);

        // Save to database
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoDetectionRepository>();
        await repository.SaveAsync(result);

        if (result.GekkoDetected)
        {
            _logger.LogInformation("🦎 GECKO DETECTED in {File}! Confidence: {Confidence:P2}",
                file.Name, result.Confidence);
        }
        else
        {
            _logger.LogDebug("No gecko in {File}. Label: {Label}, Confidence: {Confidence:P2}",
                file.Name, result.Label, result.Confidence);
        }
    }

    public override void Dispose()
    {
        _detector.Dispose();
        base.Dispose();
    }
}
