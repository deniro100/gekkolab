using Microsoft.AspNetCore.Mvc;
using GekkoLab.Services.Camera;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CameraController : ControllerBase
{
    private readonly ICameraCaptureProvider _cameraProvider;
    private readonly ILogger<CameraController> _logger;

    public CameraController(ICameraCaptureProvider cameraProvider, ILogger<CameraController> logger)
    {
        _cameraProvider = cameraProvider;
        _logger = logger;
    }

    /// <summary>
    /// Take a snapshot from the camera on demand
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

            _logger.LogInformation("Snapshot captured successfully, size: {Size} bytes", imageBytes.Length);
            return File(imageBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error taking camera snapshot");
            return StatusCode(500, new { message = "Error capturing snapshot" });
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

    private ObjectResult ServiceUnavailable(object value)
        => StatusCode(503, value);
}
