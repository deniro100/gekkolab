namespace GekkoLab.Models;

public record Bme280DataMetadata(string ReaderType);

public record Bme280Data(
    double TemperatureCelsius,
    double Humidity,
    double MillimetersOfMercury,
    DateTime Timestamp,
    Bme280DataMetadata Metadata);
