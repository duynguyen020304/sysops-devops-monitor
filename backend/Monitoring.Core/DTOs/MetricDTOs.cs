namespace Monitoring.Core.DTOs;

public record MetricTimeSeriesDto(DateTimeOffset Timestamp, double Value);

public record ServerMetricsSummaryDto(
    List<MetricTimeSeriesDto> CpuUsage,
    List<MetricTimeSeriesDto> MemoryUsage,
    List<MetricTimeSeriesDto> DiskUsage,
    List<MetricTimeSeriesDto> NetworkRx,
    List<MetricTimeSeriesDto> NetworkTx,
    List<MetricTimeSeriesDto> LoadAverage
);

public record MetricQueryDto(DateTimeOffset? From, DateTimeOffset? To, string? Interval);
