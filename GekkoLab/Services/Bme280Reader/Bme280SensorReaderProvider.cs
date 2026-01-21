namespace GekkoLab.Services.Bme280Reader;

public class Bme280SensorReaderProvider : IBme280SensorReaderProvider
{
    private readonly Lazy<IBme280Reader> _reader;

    public Bme280SensorReaderProvider(
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _reader = new Lazy<IBme280Reader>(() =>
        {
            var logger = loggerFactory.CreateLogger<Bme280SensorReaderProvider>();
            var useSimulator = configuration.GetValue<bool>("SensorConfiguration:UseSimulator", false);

            if (useSimulator)
            {
                logger.LogInformation("Using BME280 Simulator Reader");
                return new Bme280SimulatorReader(loggerFactory.CreateLogger<Bme280SimulatorReader>());
            }
            else
            {
                logger.LogInformation("Using BME280 Hardware Reader");
                return new Bme280Reader(loggerFactory.CreateLogger<Bme280Reader>());
            }
        });
    }

    public IBme280Reader GetReader() => _reader.Value;
}