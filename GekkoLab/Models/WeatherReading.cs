namespace GekkoLab.Models;

/// <summary>
/// Represents weather data retrieved from external API
/// </summary>
public class WeatherReading
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Location { get; set; } = "Redmond";
    public string Source { get; set; } = "Open-Meteo";
}
