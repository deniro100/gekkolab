using GekkoLab.Models;
using GekkoLab.Services.Repository;
using GekkoLab.Services.WeatherReader;

namespace GekkoLab.Services;

/// <summary>
/// Background service that polls weather data from Open-Meteo API
/// and stores it in the local database
/// </summary>
public class WeatherPollingService : BackgroundService
{
    private readonly ILogger<WeatherPollingService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWeatherReader _weatherReader;
    private readonly TimeSpan _pollingInterval;
    private readonly  IConfiguration _configuration;
    private readonly string _location;
    private readonly bool _enabled;

    public WeatherPollingService(
        ILogger<WeatherPollingService> logger,
        IServiceScopeFactory scopeFactory,
        IWeatherReader weatherReader,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _weatherReader = weatherReader;
        _configuration = configuration;
        
        _pollingInterval = configuration.GetValue("WeatherConfiguration:PollingInterval", TimeSpan.FromHours(1));
        _location = configuration.GetValue("WeatherConfiguration:Location", "Redmond")!;
        _enabled = configuration.GetValue("WeatherConfiguration:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Weather polling service is disabled via configuration");
            return;
        }

        _logger.LogInformation("Weather polling service started. Polling interval: {Interval}, Location: {Location}", 
            _pollingInterval, _location);

        // Small delay to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Poll immediately on startup
        await PollWeatherDataAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_pollingInterval, stoppingToken);
            
            try
            {
                await PollWeatherDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in weather polling loop");
            }
        }
    }

    private async Task PollWeatherDataAsync()
    {
        _logger.LogDebug("Polling weather data for {Location}...", _location);

        try
        {
            var weatherData = await _weatherReader.GetCurrentWeatherAsync();

            if (!weatherData.IsValid)
            {
                _logger.LogWarning("Failed to get weather data: {Error}", weatherData.ErrorMessage);
                return;
            }

            var reading = new WeatherReading
            {
                Timestamp = weatherData.Timestamp,
                Temperature = weatherData.Temperature,
                Humidity = weatherData.Humidity,
                Latitude = weatherData.Latitude,
                Longitude = weatherData.Longitude,
                Location = _location,
                Source = "Open-Meteo"
            };

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWeatherReadingRepository>();
            await repository.SaveAsync(reading);

            _logger.LogInformation("Weather data saved: {Location} - T={Temperature}°C, H={Humidity}%",
                _location, weatherData.Temperature, weatherData.Humidity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling weather data");
        }
    }
}
