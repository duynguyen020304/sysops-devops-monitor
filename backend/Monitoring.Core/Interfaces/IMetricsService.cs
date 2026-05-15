using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;

namespace Monitoring.Core.Interfaces;

public interface IMetricsService
{
    Task<ServerMetricsSummaryDto> GetServerMetricsAsync(Guid serverId, DateTimeOffset? from, DateTimeOffset? to);
    Task<List<ServerMetric>> GetRawMetricsAsync(Guid serverId, DateTimeOffset? from, DateTimeOffset? to, int limit = 1000);
}
