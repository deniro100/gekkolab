using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace GekkoLab.Services.PerformanceMonitoring;

/// <summary>
/// Represents a single real-time metrics snapshot (stored in memory)
/// </summary>
public class MetricsSnapshot
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public long DiskUsedBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// In-memory store for real-time metrics (last 2 hours of 5-second snapshots)
/// </summary>
public interface IMetricsStore
{
    void AddSnapshot(MetricsSnapshot snapshot);
    IReadOnlyList<MetricsSnapshot> GetSnapshots(TimeSpan? duration = null);
    MetricsSnapshot? GetLatest();
    IReadOnlyList<MetricsSnapshot> GetSnapshotsForAggregation();
}

public class InMemoryMetricsStore : IMetricsStore
{
    private ConcurrentBag<MetricsSnapshot> _currentSnapshots = new();
    private readonly MemoryCache _displayCache = new(new MemoryCacheOptions());
    private readonly ConcurrentDictionary<string, byte> _cacheKeys = new();
    private readonly TimeSpan _maxRetention = TimeSpan.FromHours(2);

    public void AddSnapshot(MetricsSnapshot snapshot)
    {
        _currentSnapshots.Add(snapshot);
        
        // Add to display cache with expiry
        var key = $"snapshot_{snapshot.Timestamp.Ticks}";
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_maxRetention)
            .RegisterPostEvictionCallback((evictedKey, value, reason, state) =>
            {
                _cacheKeys.TryRemove(evictedKey.ToString()!, out _);
            });
        
        _displayCache.Set(key, snapshot, cacheOptions);
        _cacheKeys.TryAdd(key, 0);
    }

    public IReadOnlyList<MetricsSnapshot> GetSnapshots(TimeSpan? duration = null)
    {
        var cutoff = duration.HasValue
            ? DateTime.UtcNow - duration.Value
            : DateTime.MinValue;

        var snapshots = new List<MetricsSnapshot>();
        foreach (var key in _cacheKeys.Keys)
        {
            if (_displayCache.TryGetValue<MetricsSnapshot>(key, out var snapshot) && snapshot != null)
            {
                if (snapshot.Timestamp >= cutoff)
                {
                    snapshots.Add(snapshot);
                }
            }
        }

        return snapshots.OrderBy(s => s.Timestamp).ToList();
    }

    public MetricsSnapshot? GetLatest()
    {
        MetricsSnapshot? latest = null;
        foreach (var key in _cacheKeys.Keys)
        {
            if (_displayCache.TryGetValue<MetricsSnapshot>(key, out var snapshot) && snapshot != null)
            {
                if (latest == null || snapshot.Timestamp > latest.Timestamp)
                {
                    latest = snapshot;
                }
            }
        }
        return latest;
    }

    public IReadOnlyList<MetricsSnapshot> GetSnapshotsForAggregation()
    {
        // Swap current collection with a new empty one
        var snapshotsToAggregate = Interlocked.Exchange(ref _currentSnapshots, new ConcurrentBag<MetricsSnapshot>());
        
        return snapshotsToAggregate
            .OrderBy(s => s.Timestamp)
            .ToList();
    }
}
