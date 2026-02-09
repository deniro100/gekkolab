using GekkoLab.Models;

namespace GekkoLab.Services.Bme280Reader;

public interface IBme280Reader
{
    Task<Bme280Data?> ReadSensorDataAsync();
    bool IsAvailable { get; }
}