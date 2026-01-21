using GekkoLab.Models;

namespace GekkoLab.Services;

public interface IBme280Reader
{
    Task<Bme280Data>? ReadSensorDataAsync();
    bool IsAvailable { get; }
}