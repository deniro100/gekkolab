using GekkoLab.Models;
using GekkoLab.Services.Bme280Reader;
using GekkoLab.Services.Repository;

namespace GekkoLab.Services;

    public class SensorPollingService : BackgroundService
    {
        private readonly ILogger<SensorPollingService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly IBme280Reader _sensorReader;

        public SensorPollingService(
            ILogger<SensorPollingService> logger,
            IServiceScopeFactory scopeFactory,
            IBme280Reader sensorReader,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _sensorReader = sensorReader;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = _configuration.GetValue<TimeSpan>("SensorConfiguration:PollingInterval", TimeSpan.FromMinutes(1));
            
            _logger.LogInformation("Sensor polling interval set to {Interval}", interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                await ReadAndStoreSensorData();

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReadAndStoreSensorData()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISensorReadingRepository>();

                var data = await _sensorReader.ReadSensorDataAsync();
                if (data == null)
                {
                    _logger.LogWarning("Sensor data is null. Sensor may not be available. Reader type: {ReaderType}", _sensorReader.GetType().Name);
                    return;
                }
                
                var reading = new SensorReading
                {
                    Temperature = data.TemperatureCelsius,
                    Humidity = data.Humidity,
                    Pressure = data.MillimetersOfMercury,
                    Timestamp = DateTime.UtcNow,
                    Metadata = new SensorMetadata { ReaderType = data.Metadata.ReaderType },
                    IsValid = true
                };

                await repository.SaveReadingAsync(reading);
                _logger.LogInformation("Sensor data saved: T={Temperature}°C, H={Humidity}%, P={Pressure}mm", 
                    reading.Temperature, reading.Humidity, reading.Pressure);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading or storing sensor data");
            }
        }
    }