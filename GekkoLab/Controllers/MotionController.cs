﻿using Microsoft.AspNetCore.Mvc;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MotionController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MotionController> _logger;
    private readonly string _captureDirectory;

    public MotionController(IConfiguration configuration, ILogger<MotionController> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _captureDirectory = _configuration.GetValue<string>("CameraConfiguration:MotionDetection:CaptureDirectory", "gekkodata/motion-captures")!;
    }

    /// <summary>
    /// Get the latest motion capture image
    /// </summary>
    [HttpGet("latest")]
    public IActionResult GetLatestCapture()
    {
        try
        {
            if (!Directory.Exists(_captureDirectory))
            {
                _logger.LogWarning("Capture directory does not exist: {Directory}", _captureDirectory);
                return NotFound(new { message = "No captures directory found" });
            }

            var latestFile = new DirectoryInfo(_captureDirectory)
                .GetFiles("motion_*.jpg")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestFile == null)
            {
                _logger.LogWarning("No motion capture files found in: {Directory}", _captureDirectory);
                return NotFound(new { message = "No motion captures found" });
            }

            _logger.LogDebug("Returning latest capture: {File}", latestFile.Name);
            var imageBytes = System.IO.File.ReadAllBytes(latestFile.FullName);
            return File(imageBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest motion capture");
            return StatusCode(500, new { message = "Error retrieving capture" });
        }
    }

    /// <summary>
    /// Get motion capture statistics
    /// </summary>
    [HttpGet("statistics")]
    public IActionResult GetStatistics()
    {
        try
        {
            if (!Directory.Exists(_captureDirectory))
            {
                return Ok(new
                {
                    totalFiles = 0,
                    totalSizeBytes = 0L,
                    totalSizeMB = 0.0,
                    oldestCapture = (DateTime?)null,
                    newestCapture = (DateTime?)null,
                    capturesLast24Hours = 0,
                    capturesLastHour = 0
                });
            }

            var files = new DirectoryInfo(_captureDirectory)
                .GetFiles("motion_*.jpg")
                .ToList();

            if (!files.Any())
            {
                return Ok(new
                {
                    totalFiles = 0,
                    totalSizeBytes = 0L,
                    totalSizeMB = 0.0,
                    oldestCapture = (DateTime?)null,
                    newestCapture = (DateTime?)null,
                    capturesLast24Hours = 0,
                    capturesLastHour = 0
                });
            }

            var now = DateTime.UtcNow;
            var totalSize = files.Sum(f => f.Length);
            var orderedFiles = files.OrderBy(f => f.LastWriteTimeUtc).ToList();

            return Ok(new
            {
                totalFiles = files.Count,
                totalSizeBytes = totalSize,
                totalSizeMB = Math.Round(totalSize / (1024.0 * 1024.0), 2),
                oldestCapture = orderedFiles.First().LastWriteTimeUtc,
                newestCapture = orderedFiles.Last().LastWriteTimeUtc,
                capturesLast24Hours = files.Count(f => f.LastWriteTimeUtc >= now.AddHours(-24)),
                capturesLastHour = files.Count(f => f.LastWriteTimeUtc >= now.AddHours(-1))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting motion capture statistics");
            return StatusCode(500, new { message = "Error retrieving statistics" });
        }
    }

    /// <summary>
    /// Get latest capture metadata (without the image data)
    /// </summary>
    [HttpGet("latest/info")]
    public IActionResult GetLatestCaptureInfo()
    {
        try
        {
            if (!Directory.Exists(_captureDirectory))
            {
                return NotFound(new { message = "No captures directory found" });
            }

            var latestFile = new DirectoryInfo(_captureDirectory)
                .GetFiles("motion_*.jpg")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestFile == null)
            {
                return NotFound(new { message = "No motion captures found" });
            }

            return Ok(new
            {
                filename = latestFile.Name,
                timestamp = latestFile.LastWriteTimeUtc,
                sizeBytes = latestFile.Length,
                sizeMB = Math.Round(latestFile.Length / (1024.0 * 1024.0), 3)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest capture info");
            return StatusCode(500, new { message = "Error retrieving capture info" });
        }
    }

    /// <summary>
    /// Get list of recent captures
    /// </summary>
    [HttpGet("recent")]
    public IActionResult GetRecentCaptures([FromQuery] int count = 10)
    {
        try
        {
            if (!Directory.Exists(_captureDirectory))
            {
                return Ok(new List<object>());
            }

            var files = new DirectoryInfo(_captureDirectory)
                .GetFiles("motion_*.jpg")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(count)
                .Select(f => new
                {
                    filename = f.Name,
                    timestamp = f.LastWriteTimeUtc,
                    sizeBytes = f.Length
                })
                .ToList();

            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent captures");
            return StatusCode(500, new { message = "Error retrieving captures" });
        }
    }

    /// <summary>
    /// Get a specific capture by filename
    /// </summary>
    [HttpGet("capture/{filename}")]
    public IActionResult GetCaptureByFilename(string filename)
    {
        try
        {
            if (!Directory.Exists(_captureDirectory))
            {
                return NotFound(new { message = "No captures directory found" });
            }

            // Sanitize filename to prevent directory traversal
            var sanitizedFilename = Path.GetFileName(filename);
            if (string.IsNullOrEmpty(sanitizedFilename) || !sanitizedFilename.StartsWith("motion_") || !sanitizedFilename.EndsWith(".jpg"))
            {
                return BadRequest(new { message = "Invalid filename" });
            }

            var filePath = Path.Combine(_captureDirectory, sanitizedFilename);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "Capture not found" });
            }

            var imageBytes = System.IO.File.ReadAllBytes(filePath);
            return File(imageBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capture by filename: {Filename}", filename);
            return StatusCode(500, new { message = "Error retrieving capture" });
        }
    }
}
