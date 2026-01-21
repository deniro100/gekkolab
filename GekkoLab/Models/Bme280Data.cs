namespace GekkoLab.Models;

public record Bme280Data(
    double TemperatureCelsius,
    double Humidity,
    double MillimetersOfMercury,
    DateTime Timestamp);