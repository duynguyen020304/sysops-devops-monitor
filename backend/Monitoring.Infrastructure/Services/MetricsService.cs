using Microsoft.EntityFrameworkCore;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class MetricsService : IMetricsService
{
    private readonly MonitoringDbContext _db;

    public MetricsService(MonitoringDbContext db)
    {
        _db = db;
    }

    public async Task<ServerMetricsSummaryDto> GetServerMetricsAsync(
        Guid serverId, DateTimeOffset? from, DateTimeOffset? to)
    {
        var query = _db.ServerMetrics.Where(m => m.ServerId == serverId);

        if (from.HasValue)
            query = query.Where(m => m.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.Timestamp <= to.Value);

        var metrics = await query
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // Determine bucket interval based on time range
        var bucketInterval = DetermineBucketInterval(from, to);

        return new ServerMetricsSummaryDto(
            CpuUsage: AggregateTimeSeries(metrics, m => m.CpuUsagePercent, bucketInterval),
            MemoryUsage: AggregateTimeSeries(metrics, m => m.MemoryUsagePercent, bucketInterval),
            DiskUsage: AggregateTimeSeries(metrics, m => m.DiskUsagePercent, bucketInterval),
            NetworkRx: AggregateTimeSeries(metrics, m => m.NetworkRxBytesPerSecond, bucketInterval),
            NetworkTx: AggregateTimeSeries(metrics, m => m.NetworkTxBytesPerSecond, bucketInterval),
            LoadAverage: AggregateTimeSeries(metrics, m => m.LoadAverage1m, bucketInterval)
        );
    }

    public async Task<List<ServerMetric>> GetRawMetricsAsync(
        Guid serverId, DateTimeOffset? from, DateTimeOffset? to, int limit = 1000)
    {
        var query = _db.ServerMetrics.Where(m => m.ServerId == serverId);

        if (from.HasValue)
            query = query.Where(m => m.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.Timestamp <= to.Value);

        return await query
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    private static TimeSpan DetermineBucketInterval(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (!from.HasValue || !to.HasValue)
            return TimeSpan.FromMinutes(5);

        var range = to.Value - from.Value;

        if (range <= TimeSpan.FromHours(1))
            return TimeSpan.FromMinutes(1);
        if (range <= TimeSpan.FromHours(6))
            return TimeSpan.FromMinutes(5);
        if (range <= TimeSpan.FromDays(1))
            return TimeSpan.FromMinutes(15);
        if (range <= TimeSpan.FromDays(7))
            return TimeSpan.FromHours(1);
        if (range <= TimeSpan.FromDays(30))
            return TimeSpan.FromHours(6);

        return TimeSpan.FromDays(1);
    }

    private static List<MetricTimeSeriesDto> AggregateTimeSeries(
        List<ServerMetric> metrics,
        Func<ServerMetric, double> selector,
        TimeSpan bucketInterval)
    {
        if (metrics.Count == 0)
            return [];

        var buckets = metrics
            .GroupBy(m => new DateTimeOffset(
                m.Timestamp.Ticks / bucketInterval.Ticks * bucketInterval.Ticks,
                m.Timestamp.Offset))
            .Select(g => new MetricTimeSeriesDto(
                Timestamp: g.Key,
                Value: Math.Round(g.Average(selector), 2)))
            .OrderBy(p => p.Timestamp)
            .ToList();

        return buckets;
    }
}
