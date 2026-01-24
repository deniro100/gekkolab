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
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<GekkoDetectionResult?> GetLatestWithGekkoAsync()
    {
        return await _context.GekkoDetections
            .Where(r => r.GekkoDetected)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<GekkoDetectionResult>> GetHistoryAsync(DateTime from, DateTime to)
    {
        return await _context.GekkoDetections
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<GekkoDetectionResult>> GetDetectionsWithGekkoAsync(DateTime from, DateTime to)
    {
        return await _context.GekkoDetections
            .Where(r => r.Timestamp >= from && r.Timestamp <= to && r.GekkoDetected)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync();
    }

    public async Task<DetectionStatistics> GetStatisticsAsync(DateTime from, DateTime to)
    {
        var detections = await _context.GekkoDetections
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .ToListAsync();

        var totalDetections = detections.Count;
        var gekkoDetections = detections.Count(r => r.GekkoDetected);
        var lastGekko = detections
            .Where(r => r.GekkoDetected)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefault();

        return new DetectionStatistics
        {
            TotalDetections = totalDetections,
            GekkoDetections = gekkoDetections,
            GekkoDetectionRate = totalDetections > 0 ? (double)gekkoDetections / totalDetections : 0,
            AverageConfidence = detections.Any() ? detections.Average(r => r.Confidence) : 0,
            LastGekkoDetection = lastGekko?.Timestamp
        };
    }
}
