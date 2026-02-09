using Microsoft.AspNetCore.Mvc;
using GekkoLab.Services.Camera;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CameraController : ControllerBase
{
    private readonly ICameraCaptureProvider _cameraProvider;
    private readonly ILogger<CameraController> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _snapshotDirectory;

    public CameraController(ICameraCaptureProvider cameraProvider, ILogger<CameraController> logger, IConfiguration configuration)
    {
        _cameraProvider = cameraProvider;
        _logger = logger;
        _configuration = configuration;
        _snapshotDirectory = _configuration.GetValue<string>("CameraConfiguration:MotionDetection:CaptureDirectory", "gekkodata/motion-captures")!;
    }

    /// <summary>
    /// Take a snapshot from the camera on demand and save to disk
    /// </summary>
    [HttpPost("snapshot")]
    public async Task<IActionResult> TakeSnapshot()
    {
        try
        {
            var camera = _cameraProvider.GetCapture();

            if (!camera.IsAvailable)
            {
                _logger.LogWarning("Camera is not available for snapshot");
                return ServiceUnavailable(new { message = "Camera is not available" });
            }

            var imageBytes = await camera.CaptureFrameAsync();

            if (imageBytes == null || imageBytes.Length == 0)
            {
                _logger.LogWarning("Camera returned empty snapshot");
                return StatusCode(500, new { message = "Failed to capture snapshot" });
            }

            // Save to disk
            Directory.CreateDirectory(_snapshotDirectory);
            var timestamp = DateTime.UtcNow;
            var filename = $"snapshot_{timestamp:yyyyMMdd_HHmmss_fff}.jpg";
            var filepath = Path.Combine(_snapshotDirectory, filename);
            await System.IO.File.WriteAllBytesAsync(filepath, imageBytes);

            CleanupOldSnapshots();

            _logger.LogInformation("Snapshot saved: {Filename} ({Size} bytes)", filename, imageBytes.Length);

            return Ok(new
            {
                filename,
                timestamp,
                sizeBytes = imageBytes.Length,
                url = $"/api/camera/snapshot/{filename}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error taking camera snapshot");
            return StatusCode(500, new { message = "Error capturing snapshot" });
        }
    }

    /// <summary>
    /// Get the latest snapshot image
    /// </summary>
    [HttpGet("snapshot/latest")]
    public IActionResult GetLatestSnapshot()
    {
        try
        {
            if (!Directory.Exists(_snapshotDirectory))
                return NotFound(new { message = "No snapshots found" });

            var latestFile = new DirectoryInfo(_snapshotDirectory)
                .GetFiles("snapshot_*.jpg")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestFile == null)
                return NotFound(new { message = "No snapshots found" });

            var imageBytes = System.IO.File.ReadAllBytes(latestFile.FullName);
            return File(imageBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest snapshot");
            return StatusCode(500, new { message = "Error retrieving snapshot" });
        }
    }

    /// <summary>
    /// Get the latest snapshot metadata
    /// </summary>
    [HttpGet("snapshot/latest/info")]
    public IActionResult GetLatestSnapshotInfo()
    {
        try
        {
            if (!Directory.Exists(_snapshotDirectory))
                return NotFound(new { message = "No snapshots found" });

            var latestFile = new DirectoryInfo(_snapshotDirectory)
                .GetFiles("snapshot_*.jpg")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestFile == null)
                return NotFound(new { message = "No snapshots found" });

            return Ok(new
            {
                filename = latestFile.Name,
                timestamp = latestFile.LastWriteTimeUtc,
                sizeBytes = latestFile.Length,
                url = $"/api/camera/snapshot/{latestFile.Name}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest snapshot info");
            return StatusCode(500, new { message = "Error retrieving snapshot info" });
        }
    }

    /// <summary>
    /// Get a specific snapshot by filename
    /// </summary>
    [HttpGet("snapshot/{filename}")]
    public IActionResult GetSnapshotByFilename(string filename)
    {
        try
        {
            var sanitized = Path.GetFileName(filename);
            if (string.IsNullOrEmpty(sanitized) || !sanitized.StartsWith("snapshot_") || !sanitized.EndsWith(".jpg"))
                return BadRequest(new { message = "Invalid filename" });

            var filePath = Path.Combine(_snapshotDirectory, sanitized);
            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = "Snapshot not found" });

            var imageBytes = System.IO.File.ReadAllBytes(filePath);
            return File(imageBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting snapshot: {Filename}", filename);
            return StatusCode(500, new { message = "Error retrieving snapshot" });
        }
    }

    /// <summary>
    /// Get camera status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var camera = _cameraProvider.GetCapture();
            return Ok(new { available = camera.IsAvailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking camera status");
            return Ok(new { available = false });
        }
    }

    private void CleanupOldSnapshots()
    {
        try
        {
            var maxAgeDays = _configuration.GetValue<int>("CameraConfiguration:MotionDetection:MaxCaptureAgeDays", 7);
            var maxFiles = _configuration.GetValue<int>("CameraConfiguration:MotionDetection:MaxCaptureFiles", 1000);
            var directory = new DirectoryInfo(_snapshotDirectory);
            if (!directory.Exists) return;

            var cutoffDate = DateTime.UtcNow.AddDays(-maxAgeDays);
            foreach (var file in directory.GetFiles("snapshot_*.jpg").Where(f => f.CreationTimeUtc < cutoffDate))
            {
                try { file.Delete(); } catch { }
            }

            var excess = directory.GetFiles("snapshot_*.jpg")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(maxFiles);
            foreach (var file in excess)
            {
                try { file.Delete(); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during snapshot cleanup");
        }
    }

    private ObjectResult ServiceUnavailable(object value)
        => StatusCode(503, value);
}
