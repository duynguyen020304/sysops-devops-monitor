using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monitoring.Infrastructure.BackgroundServices;

public class MetricAggregationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricAggregationService> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    public MetricAggregationService(IServiceProvider serviceProvider, ILogger<MetricAggregationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricAggregationService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AggregateMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metric aggregation cycle.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }

        _logger.LogInformation("MetricAggregationService stopped.");
    }

    private async Task AggregateMetricsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running hourly metric aggregation at {Time}.", DateTimeOffset.UtcNow);

        // TODO: When an aggregation summary table is added, compute and store
        // hourly avg/min/max values from raw ServerMetric records here.
        // For now, this is a placeholder that confirms the service is running.

        await Task.CompletedTask;
    }
}
