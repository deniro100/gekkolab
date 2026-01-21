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
        private Timer? _timer;

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

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = _configuration.GetValue<int>("SensorConfiguration:PollingIntervalMinutes", 1);
            var interval = TimeSpan.FromMinutes(intervalMinutes);

            _timer = new Timer(async _ => await ReadAndStoreSensorData(), null, TimeSpan.Zero, interval);

            return Task.CompletedTask;
        }

        private async Task ReadAndStoreSensorData()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISensorReadingRepository>();

                var data = await this._sensorReader.ReadSensorDataAsync();
                
                var reading = new SensorReading
                {
                    Temperature = data.TemperatureCelsius,
                    Humidity = data.Humidity,
                    Pressure = data.MillimetersOfMercury,
                    Timestamp = DateTime.UtcNow,
                    ////IsValid = data.Temperature.HasValue && data.Humidity.HasValue && data.Pressure.HasValue,
                    ////ErrorMessage = !data.Temperature.HasValue ? "Failed to read sensor data" : null
                };

                await repository.SaveReadingAsync(reading);
                _logger.LogInformation($"Sensor data saved: T={reading.Temperature}°C, H={reading.Humidity}%, P={reading.Pressure}mm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading or storing sensor data");
            }
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }