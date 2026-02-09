using GekkoLab.Models;
using Microsoft.EntityFrameworkCore;

namespace GekkoLab.Services.Repository;

public interface IGekkoDetectionRepository
{
    Task SaveAsync(GekkoDetectionResult result);
    Task<GekkoDetectionResult?> GetLatestAsync();
    Task<GekkoDetectionResult?> GetLatestWithGekkoAsync();
    Task<IEnumerable<GekkoDetectionResult>> GetHistoryAsync(DateTime from, DateTime to);
    Task<IEnumerable<GekkoDetectionResult>> GetDetectionsWithGekkoAsync(DateTime from, DateTime to);
    Task<DetectionStatistics> GetStatisticsAsync(DateTime from, DateTime to);
}

public class DetectionStatistics
{
    public int TotalDetections { get; set; }
    public int GekkoDetections { get; set; }
    public double GekkoDetectionRate { get; set; }
    public float AverageConfidence { get; set; }
    public DateTime? LastGekkoDetection { get; set; }
}

public class GekkoDetectionRepository : IGekkoDetectionRepository
{
    private readonly GekkoLabDbContext _context;
    private readonly ILogger<GekkoDetectionRepository> _logger;

    public GekkoDetectionRepository(GekkoLabDbContext context, ILogger<GekkoDetectionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveAsync(GekkoDetectionResult result)
    {
        _context.GekkoDetections.Add(result);
        await _context.SaveChangesAsync();
        _logger.LogDebug("Saved gecko detection: {Label} with confidence {Confidence:P2}, GekkoDetected: {Detected}",
            result.Label, result.Confidence, result.GekkoDetected);
    }

    public async Task<GekkoDetectionResult?> GetLatestAsync()
    {
        return await _context.GekkoDetections
            .AsNoTracking()
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<GekkoDetectionResult?> GetLatestWithGekkoAsync()
    {
        return await _context.GekkoDetections
            .AsNoTracking()
            .Where(r => r.GekkoDetected)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<GekkoDetectionResult>> GetHistoryAsync(DateTime from, DateTime to)
    {
        return await _context.GekkoDetections
            .AsNoTracking()
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<GekkoDetectionResult>> GetDetectionsWithGekkoAsync(DateTime from, DateTime to)
    {
        return await _context.GekkoDetections
            .AsNoTracking()
            .Where(r => r.Timestamp >= from && r.Timestamp <= to && r.GekkoDetected)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync();
    }

    public async Task<DetectionStatistics> GetStatisticsAsync(DateTime from, DateTime to)
    {
        var query = _context.GekkoDetections
            .AsNoTracking()
            .Where(r => r.Timestamp >= from && r.Timestamp <= to);

        var totalDetections = await query.CountAsync();

        if (totalDetections == 0)
        {
            return new DetectionStatistics();
        }

        var gekkoDetections = await query.CountAsync(r => r.GekkoDetected);
        var averageConfidence = await query.AverageAsync(r => (double)r.Confidence);
        var lastGekkoDetection = await query
            .Where(r => r.GekkoDetected)
            .OrderByDescending(r => r.Timestamp)
            .Select(r => (DateTime?)r.Timestamp)
            .FirstOrDefaultAsync();

        return new DetectionStatistics
        {
            TotalDetections = totalDetections,
            GekkoDetections = gekkoDetections,
            GekkoDetectionRate = (double)gekkoDetections / totalDetections,
            AverageConfidence = (float)averageConfidence,
            LastGekkoDetection = lastGekkoDetection
        };
    }
}
