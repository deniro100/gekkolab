using GekkoLab.Models;

namespace GekkoLab.Services.Bme280Reader;

public class Bme280SimulatorReader : IBme280Reader
{
    private readonly ILogger<Bme280SimulatorReader> _logger;
    private readonly Random _random = new();

    public Bme280SimulatorReader(ILogger<Bme280SimulatorReader> logger)
    {
        _logger = logger;
    }

    public Task<Bme280Data?> ReadSensorDataAsync()
    {
        var data = new Bme280Data(
            TemperatureCelsius: 20 + _random.NextDouble() * 10,
            Humidity: 40 + _random.NextDouble() * 30,
            MillimetersOfMercury: 740 + _random.NextDouble() * 40,
            Timestamp: DateTime.UtcNow,
            Metadata: new Bme280DataMetadata(ReaderType: "simulator"));

        _logger.LogDebug("Simulated sensor data: T={Temp}°C, H={Hum}%, P={Press}mm",
            data.TemperatureCelsius, data.Humidity, data.MillimetersOfMercury);

        return Task.FromResult<Bme280Data?>(data);
    }

    public bool IsAvailable => true;
}