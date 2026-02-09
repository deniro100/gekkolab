using System.Globalization;
using System.Text.Json;

namespace GekkoLab.Services.WeatherReader;

/// <summary>
/// Response model for Open-Meteo API
/// </summary>
public class OpenMeteoResponse
{
    public double latitude { get; set; }
    public double longitude { get; set; }
    public OpenMeteoCurrentData? current { get; set; }
}

public class OpenMeteoCurrentData
{
    public string? time { get; set; }
    public double temperature_2m { get; set; }
    public double relative_humidity_2m { get; set; }
}

/// <summary>
/// Weather data result from the reader
/// </summary>
public class WeatherData
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Interface for weather data reader
/// </summary>
public interface IWeatherReader
{
    Task<WeatherData> GetCurrentWeatherAsync();
}

/// <summary>
/// Weather reader that fetches data from Open-Meteo API for Redmond, WA
/// </summary>
public class OpenMeteoWeatherReader : IWeatherReader, IDisposable
{
    private readonly ILogger<OpenMeteoWeatherReader> _logger;
    private readonly HttpClient _httpClient;
    private readonly double _latitude;
    private readonly double _longitude;
    private readonly bool _disposeHttpClient;

    // Redmond, WA coordinates
    public const double DefaultLatitude = 47.67;
    public const double DefaultLongitude = -122.12;

    public OpenMeteoWeatherReader(
        ILogger<OpenMeteoWeatherReader> logger,
        IConfiguration configuration)
        : this(logger, configuration, new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        }) { Timeout = TimeSpan.FromSeconds(30) })
    {
        _disposeHttpClient = true;
    }

    /// <summary>
    /// Constructor for testing with custom HttpClient
    /// </summary>
    public OpenMeteoWeatherReader(
        ILogger<OpenMeteoWeatherReader> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        
        _latitude = configuration.GetValue("WeatherConfiguration:Latitude", DefaultLatitude);
        _longitude = configuration.GetValue("WeatherConfiguration:Longitude", DefaultLongitude);
    }

    public async Task<WeatherData> GetCurrentWeatherAsync()
    {
        try
        {
            // Use InvariantCulture to ensure decimal point (not comma) in URL
            var latStr = _latitude.ToString(CultureInfo.InvariantCulture);
            var lonStr = _longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m,relative_humidity_2m";
            
            _logger.LogDebug("Fetching weather data from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OpenMeteoResponse>(json);
            
            if (data?.current == null)
            {
                _logger.LogWarning("Invalid response from weather API: {Json}", json);
                return new WeatherData
                {
                    IsValid = false,
                    ErrorMessage = "Invalid response from weather API"
                };
            }

            _logger.LogDebug("Weather data received: T={Temperature}°C, H={Humidity}%", 
                data.current.temperature_2m, data.current.relative_humidity_2m);

            return new WeatherData
            {
                Temperature = data.current.temperature_2m,
                Humidity = data.current.relative_humidity_2m,
                Latitude = data.latitude,
                Longitude = data.longitude,
                Timestamp = DateTime.UtcNow,
                IsValid = true
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching weather data");
            return new WeatherData
            {
                IsValid = false,
                ErrorMessage = $"HTTP error: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout fetching weather data");
            return new WeatherData
            {
                IsValid = false,
                ErrorMessage = "Request timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data");
            return new WeatherData
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
