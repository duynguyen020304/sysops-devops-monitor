using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.Core.Enums;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.BackgroundServices;

public class LogRetentionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LogRetentionService> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    public LogRetentionService(IServiceProvider serviceProvider, ILogger<LogRetentionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogRetentionService started.");

        // Wait until 3 AM on the first run, then loop every 24 hours
        await WaitUntilNext3AmAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log retention cycle.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }

        _logger.LogInformation("LogRetentionService stopped.");
    }

    private async Task RunRetentionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running log retention cleanup at {Time}.", DateTimeOffset.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

        var now = DateTimeOffset.UtcNow;

        // 1. Delete WorkflowLog older than 30 days
        var workflowCutoff = now.AddDays(-30);
        var oldWorkflowLogs = await db.WorkflowLogs
            .Where(l => l.Timestamp < workflowCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("Deleted {Count} workflow logs older than 30 days.", oldWorkflowLogs);

        // 2. Delete PM2Log (non-error) older than 14 days
        var pm2NonErrorCutoff = now.AddDays(-14);
        var oldPm2NonErrorLogs = await db.PM2Logs
            .Where(l => l.Level != "error" && l.Timestamp < pm2NonErrorCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("Deleted {Count} non-error PM2 logs older than 14 days.", oldPm2NonErrorLogs);

        // 3. Delete PM2Log with Level=error older than 60 days
        var pm2ErrorCutoff = now.AddDays(-60);
        var oldPm2ErrorLogs = await db.PM2Logs
            .Where(l => l.Level == "error" && l.Timestamp < pm2ErrorCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("Deleted {Count} error PM2 logs older than 60 days.", oldPm2ErrorLogs);

        // 4. Delete Alert with Status=Resolved older than 180 days
        var alertCutoff = now.AddDays(-180);
        var oldAlerts = await db.Alerts
            .Where(a => a.Status == AlertStatus.Resolved && a.ResolvedAt < alertCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("Deleted {Count} resolved alerts older than 180 days.", oldAlerts);

        // 5. Delete ServerMetric older than 365 days
        var metricCutoff = now.AddDays(-365);
        var oldMetrics = await db.ServerMetrics
            .Where(m => m.Timestamp < metricCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation("Deleted {Count} server metrics older than 365 days.", oldMetrics);

        _logger.LogInformation("Log retention cleanup completed.");
    }

    private static async Task WaitUntilNext3AmAsync(CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;
        var next3Am = now.Date.AddHours(3); // 3 AM UTC today

        if (now >= next3Am)
            next3Am = next3Am.AddDays(1);

        var delay = next3Am - now;
        await Task.Delay(delay, stoppingToken);
    }
}
