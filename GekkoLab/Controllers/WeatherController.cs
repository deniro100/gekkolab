using GekkoLab.Services.Repository;
using GekkoLab.Services.WeatherReader;
using Microsoft.AspNetCore.Mvc;

namespace GekkoLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWeatherReader _weatherReader;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(
        IServiceScopeFactory scopeFactory,
        IWeatherReader weatherReader,
        ILogger<WeatherController> logger)
    {
        _scopeFactory = scopeFactory;
        _weatherReader = weatherReader;
        _logger = logger;
    }

    /// <summary>
    /// Get the latest weather reading from the database
    /// </summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWeatherReadingRepository>();
        var reading = await repository.GetLatestAsync();
        
        if (reading == null)
            return NotFound(new { message = "No weather data available" });
        
        return Ok(reading);
    }

    /// <summary>
    /// Get current weather directly from the API (not from database)
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var weatherData = await _weatherReader.GetCurrentWeatherAsync();
        
        if (!weatherData.IsValid)
            return StatusCode(503, new { message = "Weather service unavailable", error = weatherData.ErrorMessage });
        
        return Ok(new
        {
            temperature = weatherData.Temperature,
            humidity = weatherData.Humidity,
            latitude = weatherData.Latitude,
            longitude = weatherData.Longitude,
            timestamp = weatherData.Timestamp
        });
    }

    /// <summary>
    /// Get weather history from the database
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
        
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWeatherReadingRepository>();
        var readings = await repository.GetHistoryAsync(fromDate, toDate);
        
        return Ok(readings);
    }
}
