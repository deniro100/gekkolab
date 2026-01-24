using GekkoLab.Services.GekkoDetector;
using GekkoLab.Services.Repository;
using Microsoft.AspNetCore.Mvc;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GekkoDetectionController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGekkoDetector _detector;
    private readonly ILogger<GekkoDetectionController> _logger;

    public GekkoDetectionController(
        IServiceScopeFactory scopeFactory,
        IGekkoDetectorProvider detectorProvider,
        ILogger<GekkoDetectionController> logger)
    {
        _scopeFactory = scopeFactory;
        _detector = detectorProvider.GetDetector();
        _logger = logger;
    }

    /// <summary>
    /// Get the latest detection result
    /// </summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoDetectionRepository>();
        var result = await repository.GetLatestAsync();

        if (result == null)
            return NotFound(new { message = "No detection results available" });

        return Ok(result);
    }

    /// <summary>
    /// Get the latest detection where a gecko was found
    /// </summary>
    [HttpGet("latest-gecko")]
    public async Task<IActionResult> GetLatestWithGekko()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoDetectionRepository>();
        var result = await repository.GetLatestWithGekkoAsync();

        if (result == null)
            return NotFound(new { message = "No gecko detections found" });

        return Ok(result);
    }

    /// <summary>
    /// Get detection history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddHours(-24);
        var toDate = to ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoDetectionRepository>();
        var results = await repository.GetHistoryAsync(fromDate, toDate);

        return Ok(results);
    }

    /// <summary>
    /// Get only detections where gecko was found
    /// </summary>
    [HttpGet("gecko-detections")]
    public async Task<IActionResult> GetGekkoDetections([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoDetectionRepository>();
        var results = await repository.GetDetectionsWithGekkoAsync(fromDate, toDate);

        return Ok(results);
    }

    /// <summary>
    /// Get detection statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoDetectionRepository>();
        var stats = await repository.GetStatisticsAsync(fromDate, toDate);

        return Ok(stats);
    }

    /// <summary>
    /// Get detector status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            modelLoaded = _detector.IsModelLoaded,
            detectorType = _detector.GetType().Name
        });
    }
}
