using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using GekkoLab.Models;
using GekkoLab.Services.Repository;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorController : ControllerBase
{
    private readonly ISensorReadingRepository _repository;
    private readonly ILogger<SensorController> _logger;

    public SensorController(ISensorReadingRepository repository, ILogger<SensorController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        var reading = await _repository.GetLatestReadingAsync();
        if (reading == null)
            return NotFound("No readings available");
            
        return Ok(reading);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
            
        var readings = await _repository.GetReadingsByDateRangeAsync(fromDate, toDate);
        return Ok(readings);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] string metric = "temperature", [FromQuery] int days = 7)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-days);
            
        var averages = await _repository.GetDailyAveragesAsync(metric, from, to);
        var totalCount = await _repository.GetTotalReadingsCountAsync();
            
        return Ok(new 
        {
            Metric = metric,
            DailyAverages = averages,
            TotalReadings = totalCount,
            Period = new { From = from, To = to }
        });
    }
}