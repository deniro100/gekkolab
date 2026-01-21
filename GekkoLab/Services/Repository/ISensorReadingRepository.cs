using GekkoLab.Models;

namespace GekkoLab.Services.Repository;

public interface ISensorReadingRepository
{
    Task<SensorReading> SaveReadingAsync(SensorReading reading);
    Task<SensorReading?> GetLatestReadingAsync();
    Task<List<SensorReading>> GetReadingsByDateRangeAsync(DateTime from, DateTime to);
    Task<Dictionary<DateTime, double>> GetDailyAveragesAsync(string metric, DateTime from, DateTime to);
    Task<int> GetTotalReadingsCountAsync();
}