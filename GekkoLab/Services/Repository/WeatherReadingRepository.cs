using GekkoLab.Models;
using Microsoft.EntityFrameworkCore;

namespace GekkoLab.Services.Repository;

public interface IWeatherReadingRepository
{
    Task SaveAsync(WeatherReading reading);
    Task<WeatherReading?> GetLatestAsync();
    Task<IEnumerable<WeatherReading>> GetHistoryAsync(DateTime from, DateTime to);
}

public class WeatherReadingRepository : IWeatherReadingRepository
{
    private readonly GekkoLabDbContext _context;
    private readonly ILogger<WeatherReadingRepository> _logger;

    public WeatherReadingRepository(GekkoLabDbContext context, ILogger<WeatherReadingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveAsync(WeatherReading reading)
    {
        _context.WeatherReadings.Add(reading);
        await _context.SaveChangesAsync();
        _logger.LogDebug("Saved weather reading: T={Temperature}°C, H={Humidity}%", 
            reading.Temperature, reading.Humidity);
    }

    public async Task<WeatherReading?> GetLatestAsync()
    {
        return await _context.WeatherReadings
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WeatherReading>> GetHistoryAsync(DateTime from, DateTime to)
    {
        return await _context.WeatherReadings
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();
    }
}
