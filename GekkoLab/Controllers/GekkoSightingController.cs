using GekkoLab.Services.Repository;
using Microsoft.AspNetCore.Mvc;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GekkoSightingController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GekkoSightingController> _logger;

    public GekkoSightingController(
        IServiceScopeFactory scopeFactory,
        ILogger<GekkoSightingController> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get the latest gecko sighting
    /// </summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoSightingRepository>();
        var sighting = await repository.GetLatestAsync();

        if (sighting == null)
            return NotFound(new { message = "No gecko sightings recorded yet" });

        return Ok(sighting);
    }

    /// <summary>
    /// Get gecko sighting history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoSightingRepository>();
        var sightings = await repository.GetHistoryAsync(fromDate, toDate);

        return Ok(sightings);
    }

    /// <summary>
    /// Get gecko sighting statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoSightingRepository>();
        var stats = await repository.GetStatisticsAsync(fromDate, toDate);

        return Ok(stats);
    }

    /// <summary>
    /// Get count of gecko sightings in a time range
    /// </summary>
    [HttpGet("count")]
    public async Task<IActionResult> GetCount([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGekkoSightingRepository>();
        var count = await repository.GetCountAsync(fromDate, toDate);

        return Ok(new { count, from = fromDate, to = toDate });
    }
}
