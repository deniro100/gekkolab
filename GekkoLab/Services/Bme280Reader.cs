using System.Device.I2c;
using GekkoLab.Models;
using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.ReadResult;

namespace GekkoLab.Services;

public class Bme280Reader : IBme280Reader, IDisposable
{
    private const int I2cBusId = 1;
    private const int Bme280Address = 0x76; // Default address, can also be 0x77
    
    private readonly I2cDevice? _i2cDevice;
    private readonly Bme280? _bme280;
    private bool _disposed;

    public bool IsAvailable => _bme280 != null;

    public Bme280Reader()
    {
        try
        {
            var i2cSettings = new I2cConnectionSettings(I2cBusId, Bme280Address);
            _i2cDevice = I2cDevice.Create(i2cSettings);
            _bme280 = new Bme280(_i2cDevice);
            
            // Set sampling and filtering
            _bme280.TemperatureSampling = Sampling.UltraHighResolution;
            _bme280.PressureSampling = Sampling.UltraHighResolution;
            _bme280.HumiditySampling = Sampling.UltraHighResolution;
        }
        catch (Exception)
        {
            _i2cDevice?.Dispose();
            _i2cDevice = null;
            _bme280 = null;
        }
    }

    public Task<Bme280Data>? ReadSensorDataAsync()
    {
        if (_bme280 == null)
        {
            return null;
        }

        try
        {
            var result = _bme280.Read();
            
            if (!result.Temperature.HasValue  || !result.Humidity.HasValue || !result.Pressure.HasValue)
            {
                return null;
            }

            return Task.FromResult(new Bme280Data(
                TemperatureCelsius: result.Temperature.Value.DegreesCelsius,
                Humidity: result.Humidity.Value.Percent,
                MillimetersOfMercury: result.Pressure.Value.MillimetersOfMercury,
                Timestamp: DateTime.UtcNow));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _bme280?.Dispose();
        _i2cDevice?.Dispose();
        _disposed = true;
    }
}