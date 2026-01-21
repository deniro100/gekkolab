using GekkoLab.Models;
using Microsoft.EntityFrameworkCore;

namespace GekkoLab.Services.Repository;

    public class SensorReadingRepository : ISensorReadingRepository
    {
        private readonly GekkoLabDbContext _context;

        public SensorReadingRepository(GekkoLabDbContext context)
        {
            _context = context;
        }

        public async Task<SensorReading> SaveReadingAsync(SensorReading reading)
        {
            _context.SensorReadings.Add(reading);
            await _context.SaveChangesAsync();
            return reading;
        }

        public async Task<SensorReading?> GetLatestReadingAsync()
        {
            return await _context.SensorReadings
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task<List<SensorReading>> GetReadingsByDateRangeAsync(DateTime from, DateTime to)
        {
            return await _context.SensorReadings
                .Where(r => r.Timestamp >= from && r.Timestamp <= to)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();
        }

        public async Task<Dictionary<DateTime, double>> GetDailyAveragesAsync(string metric, DateTime from, DateTime to)
        {
            var query = _context.SensorReadings
                .Where(r => r.Timestamp >= from && r.Timestamp <= to && r.IsValid)
                .GroupBy(r => r.Timestamp.Date);

            return metric.ToLower() switch
            {
                "temperature" => await query.Select(g => new { Date = g.Key, Avg = g.Average(r => r.Temperature) })
                    .ToDictionaryAsync(x => x.Date, x => x.Avg),
                "humidity" => await query.Select(g => new { Date = g.Key, Avg = g.Average(r => r.Humidity) })
                    .ToDictionaryAsync(x => x.Date, x => x.Avg),
                "pressure" => await query.Select(g => new { Date = g.Key, Avg = g.Average(r => r.Pressure) })
                    .ToDictionaryAsync(x => x.Date, x => x.Avg),
                _ => new Dictionary<DateTime, double>()
            };
        }

        public async Task<int> GetTotalReadingsCountAsync()
        {
            return await _context.SensorReadings.CountAsync();
        }
    }