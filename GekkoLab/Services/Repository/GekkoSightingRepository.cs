using GekkoLab.Models;
using Microsoft.EntityFrameworkCore;

namespace GekkoLab.Services.Repository;

public interface IGekkoSightingRepository
{
    Task SaveAsync(GekkoSighting sighting);
    Task<GekkoSighting?> GetLatestAsync();
    Task<IEnumerable<GekkoSighting>> GetHistoryAsync(DateTime from, DateTime to);
    Task<int> GetCountAsync(DateTime from, DateTime to);
    Task<GekkoSightingStatistics> GetStatisticsAsync(DateTime from, DateTime to);
}

public class GekkoSightingStatistics
{
    public int TotalSightings { get; set; }
    public int SightingsLast24Hours { get; set; }
    public int SightingsLastHour { get; set; }
    public float AverageConfidence { get; set; }
    public float MaxConfidence { get; set; }
    public DateTime? FirstSighting { get; set; }
    public DateTime? LastSighting { get; set; }
}

public class GekkoSightingRepository : IGekkoSightingRepository
{
    private readonly GekkoLabDbContext _context;
    private readonly ILogger<GekkoSightingRepository> _logger;

    public GekkoSightingRepository(GekkoLabDbContext context, ILogger<GekkoSightingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveAsync(GekkoSighting sighting)
    {
        _context.GekkoSightings.Add(sighting);
        await _context.SaveChangesAsync();
        _logger.LogInformation("🦎 Gecko sighting saved: Confidence={Confidence:P2}, Position=({X},{Y})",
            sighting.Confidence, sighting.PositionX, sighting.PositionY);
    }

    public async Task<GekkoSighting?> GetLatestAsync()
    {
        return await _context.GekkoSightings
            .AsNoTracking()
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<GekkoSighting>> GetHistoryAsync(DateTime from, DateTime to)
    {
        return await _context.GekkoSightings
            .AsNoTracking()
            .Where(s => s.Timestamp >= from && s.Timestamp <= to)
            .OrderByDescending(s => s.Timestamp)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(DateTime from, DateTime to)
    {
        return await _context.GekkoSightings
            .Where(s => s.Timestamp >= from && s.Timestamp <= to)
            .CountAsync();
    }

    public async Task<GekkoSightingStatistics> GetStatisticsAsync(DateTime from, DateTime to)
    {
        var query = _context.GekkoSightings
            .AsNoTracking()
            .Where(s => s.Timestamp >= from && s.Timestamp <= to);

        var totalSightings = await query.CountAsync();

        if (totalSightings == 0)
        {
            return new GekkoSightingStatistics();
        }

        var now = DateTime.UtcNow;

        return new GekkoSightingStatistics
        {
            TotalSightings = totalSightings,
            SightingsLast24Hours = await query.CountAsync(s => s.Timestamp >= now.AddHours(-24)),
            SightingsLastHour = await query.CountAsync(s => s.Timestamp >= now.AddHours(-1)),
            AverageConfidence = (float)await query.AverageAsync(s => (double)s.Confidence),
            MaxConfidence = (float)await query.MaxAsync(s => (double)s.Confidence),
            FirstSighting = await query.OrderBy(s => s.Timestamp).Select(s => (DateTime?)s.Timestamp).FirstOrDefaultAsync(),
            LastSighting = await query.OrderByDescending(s => s.Timestamp).Select(s => (DateTime?)s.Timestamp).FirstOrDefaultAsync()
        };
    }
}
