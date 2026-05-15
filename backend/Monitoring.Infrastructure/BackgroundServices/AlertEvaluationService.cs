using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.BackgroundServices;

public class AlertEvaluationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertEvaluationService> _logger;
    private static readonly TimeSpan EvaluationInterval = TimeSpan.FromSeconds(60);

    public AlertEvaluationService(IServiceProvider serviceProvider, ILogger<AlertEvaluationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertEvaluationService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAllRulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during alert evaluation cycle.");
            }

            await Task.Delay(EvaluationInterval, stoppingToken);
        }

        _logger.LogInformation("AlertEvaluationService stopped.");
    }

    private async Task EvaluateAllRulesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

        var rules = await db.AlertRules
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
            return;

        _logger.LogDebug("Evaluating {Count} alert rules...", rules.Count);

        foreach (var rule in rules)
        {
            try
            {
                await EvaluateRuleAsync(db, rule, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate alert rule {RuleName} (Id: {RuleId}).", rule.Name, rule.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EvaluateRuleAsync(MonitoringDbContext db, AlertRule rule, CancellationToken cancellationToken)
    {
        switch (rule.SourceType)
        {
            case AlertSourceType.PM2:
                await EvaluatePM2RulesAsync(db, rule, cancellationToken);
                break;
            case AlertSourceType.Server:
                await EvaluateServerRulesAsync(db, rule, cancellationToken);
                break;
            case AlertSourceType.GitHubActions:
                await EvaluateGitHubRulesAsync(db, rule, cancellationToken);
                break;
        }
    }

    private async Task EvaluatePM2RulesAsync(MonitoringDbContext db, AlertRule rule, CancellationToken cancellationToken)
    {
        var servers = await db.Servers
            .Where(s => s.WorkspaceId == rule.WorkspaceId)
            .ToListAsync(cancellationToken);

        foreach (var server in servers)
        {
            var processes = await db.PM2Processes
                .Where(p => p.ServerId == server.Id)
                .ToListAsync(cancellationToken);

            foreach (var process in processes)
            {
                switch (rule.ConditionType)
                {
                    case "process_stopped":
                        if (process.Status == PM2ProcessStatus.Stopped)
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"PM2 process stopped: {process.Name}",
                                $"Process {process.Name} (PM2 ID: {process.Pm2Id}) on {server.Hostname} is stopped.",
                                AlertSeverity.Critical,
                                process.Id);
                        }
                        break;

                    case "process_errored":
                        if (process.Status == PM2ProcessStatus.Errored)
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"PM2 process errored: {process.Name}",
                                $"Process {process.Name} (PM2 ID: {process.Pm2Id}) on {server.Hostname} is in error state.",
                                AlertSeverity.Critical,
                                process.Id);
                        }
                        break;

                    case "high_restart_count":
                        if (process.RestartCount > (int)rule.Threshold)
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"High restart count: {process.Name}",
                                $"Process {process.Name} on {server.Hostname} has restarted {process.RestartCount} times (threshold: {rule.Threshold}).",
                                AlertSeverity.Warning,
                                process.Id);
                        }
                        break;

                    case "high_memory":
                        var memoryMb = process.MemoryUsage / (1024.0 * 1024.0);
                        if (memoryMb > rule.Threshold)
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"High memory usage: {process.Name}",
                                $"Process {process.Name} on {server.Hostname} is using {memoryMb:F0}MB (threshold: {rule.Threshold}MB).",
                                AlertSeverity.Warning,
                                process.Id);
                        }
                        break;

                    case "high_cpu":
                        if (process.CpuUsage > rule.Threshold)
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"High CPU usage: {process.Name}",
                                $"Process {process.Name} on {server.Hostname} CPU usage is {process.CpuUsage:F1}% (threshold: {rule.Threshold}%).",
                                AlertSeverity.Warning,
                                process.Id);
                        }
                        break;
                }
            }
        }
    }

    private async Task EvaluateServerRulesAsync(MonitoringDbContext db, AlertRule rule, CancellationToken cancellationToken)
    {
        var servers = await db.Servers
            .Where(s => s.WorkspaceId == rule.WorkspaceId)
            .ToListAsync(cancellationToken);

        foreach (var server in servers)
        {
            switch (rule.ConditionType)
            {
                case "agent_offline":
                    if (server.LastHeartbeatAt.HasValue)
                    {
                        var heartbeatAge = DateTimeOffset.UtcNow - server.LastHeartbeatAt.Value;
                        if (heartbeatAge > TimeSpan.FromSeconds(rule.TimeWindowSeconds))
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"Agent offline: {server.Hostname}",
                                $"Server {server.Hostname} ({server.IpAddress}) has not sent a heartbeat for {heartbeatAge.TotalMinutes:F0} minutes (threshold: {rule.TimeWindowSeconds / 60} minutes).",
                                rule.Severity,
                                server.Id);
                        }
                    }
                    else
                    {
                        // No heartbeat ever received
                        var age = DateTimeOffset.UtcNow - new DateTimeOffset(server.CreatedAt, TimeSpan.Zero);
                        if (age > TimeSpan.FromSeconds(rule.TimeWindowSeconds))
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"Agent never connected: {server.Hostname}",
                                $"Server {server.Hostname} ({server.IpAddress}) has never sent a heartbeat since registration.",
                                rule.Severity,
                                server.Id);
                        }
                    }
                    break;

                case "high_cpu":
                case "critical_cpu":
                case "high_ram":
                case "critical_ram":
                case "high_disk":
                case "critical_disk":
                    await EvaluateServerMetricRuleAsync(db, rule, server, cancellationToken);
                    break;
            }
        }
    }

    private async Task EvaluateServerMetricRuleAsync(
        MonitoringDbContext db, AlertRule rule, Server server, CancellationToken cancellationToken)
    {
        var windowStart = DateTimeOffset.UtcNow.AddSeconds(-rule.TimeWindowSeconds);

        var metrics = await db.ServerMetrics
            .Where(m => m.ServerId == server.Id && m.Timestamp >= windowStart)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync(cancellationToken);

        if (metrics.Count == 0)
            return;

        double metricValue = rule.ConditionType switch
        {
            "high_cpu" or "critical_cpu" => metrics.Average(m => m.CpuUsagePercent),
            "high_ram" or "critical_ram" => metrics.Average(m => m.MemoryUsagePercent),
            "high_disk" or "critical_disk" => metrics.Max(m => m.DiskUsagePercent),
            _ => 0
        };

        string metricName = rule.ConditionType switch
        {
            "high_cpu" or "critical_cpu" => "CPU",
            "high_ram" or "critical_ram" => "RAM",
            "high_disk" or "critical_disk" => "Disk",
            _ => "Unknown"
        };

        if (metricValue > rule.Threshold)
        {
            var unit = rule.ConditionType.Contains("disk") ? "current" : $"average over {rule.TimeWindowSeconds / 60}min";
            await CreateAlertIfNotInCooldownAsync(db, rule,
                $"High {metricName} usage: {server.Hostname}",
                $"Server {server.Hostname} {metricName} usage {unit} is {metricValue:F1}% (threshold: {rule.Threshold}%).",
                rule.Severity,
                server.Id);
        }
    }

    private async Task EvaluateGitHubRulesAsync(MonitoringDbContext db, AlertRule rule, CancellationToken cancellationToken)
    {
        var repositories = await db.Repositories
            .Where(r => r.WorkspaceId == rule.WorkspaceId)
            .ToListAsync(cancellationToken);

        foreach (var repo in repositories)
        {
            var windowStart = DateTimeOffset.UtcNow.AddSeconds(-rule.TimeWindowSeconds);

            var recentRuns = await db.WorkflowRuns
                .Where(r => r.RepositoryId == repo.Id && r.StartedAt >= windowStart)
                .ToListAsync(cancellationToken);

            switch (rule.ConditionType)
            {
                case "workflow_failed":
                    var failedRuns = recentRuns.Where(r => r.Conclusion == "failure").ToList();
                    foreach (var run in failedRuns)
                    {
                        await CreateAlertIfNotInCooldownAsync(db, rule,
                            $"Workflow failed: {run.WorkflowName}",
                            $"Workflow '{run.WorkflowName}' on {repo.FullName} (branch: {run.Branch}) failed. Actor: {run.Actor}.",
                            AlertSeverity.Warning,
                            run.Id);
                    }
                    break;

                case "deployment_failed":
                    var deploymentFailures = recentRuns
                        .Where(r => r.Conclusion == "failure" &&
                                    (r.WorkflowName.Contains("deploy", StringComparison.OrdinalIgnoreCase) ||
                                     r.WorkflowName.Contains("release", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    foreach (var run in deploymentFailures)
                    {
                        await CreateAlertIfNotInCooldownAsync(db, rule,
                            $"Deployment failed: {run.WorkflowName}",
                            $"Deployment workflow '{run.WorkflowName}' on {repo.FullName} (branch: {run.Branch}) failed. Actor: {run.Actor}.",
                            AlertSeverity.Critical,
                            run.Id);
                    }
                    break;

                case "duration_anomaly":
                    var completedRuns = recentRuns
                        .Where(r => r.Conclusion == "success" && r.DurationSeconds.HasValue)
                        .ToList();
                    if (completedRuns.Count >= 3)
                    {
                        var avgDuration = completedRuns.Average(r => r.DurationSeconds!.Value);
                        var thresholdDuration = avgDuration * rule.Threshold;

                        var slowRuns = completedRuns.Where(r => r.DurationSeconds!.Value > thresholdDuration).ToList();
                        foreach (var run in slowRuns)
                        {
                            await CreateAlertIfNotInCooldownAsync(db, rule,
                                $"Slow workflow: {run.WorkflowName}",
                                $"Workflow '{run.WorkflowName}' on {repo.FullName} took {run.DurationSeconds}s, which is {rule.Threshold:F1}x the average ({avgDuration:F0}s).",
                                AlertSeverity.Warning,
                                run.Id);
                        }
                    }
                    break;
            }
        }
    }

    private async Task CreateAlertIfNotInCooldownAsync(
        MonitoringDbContext db,
        AlertRule rule,
        string title,
        string description,
        AlertSeverity severity,
        Guid sourceId)
    {
        var cooldownStart = DateTimeOffset.UtcNow.AddSeconds(-rule.CooldownSeconds);

        var existingAlert = await db.Alerts.FirstOrDefaultAsync(a =>
            a.RuleId == rule.Id
            && a.SourceId == sourceId
            && a.Title == title
            && a.Status != AlertStatus.Resolved
            && a.TriggeredAt >= cooldownStart);

        if (existingAlert is not null)
            return;

        // Also check if there's a recently resolved alert within cooldown to avoid flapping
        var recentlyResolved = await db.Alerts.FirstOrDefaultAsync(a =>
            a.RuleId == rule.Id
            && a.SourceId == sourceId
            && a.Title == title
            && a.Status == AlertStatus.Resolved
            && a.ResolvedAt >= cooldownStart);

        if (recentlyResolved is not null)
            return;

        _logger.LogInformation("Creating alert: {Title} (Rule: {RuleName})", title, rule.Name);

        db.Alerts.Add(new Alert
        {
            Id = Guid.NewGuid(),
            WorkspaceId = rule.WorkspaceId,
            SourceType = rule.SourceType,
            SourceId = sourceId,
            Title = title,
            Description = description,
            Severity = severity,
            Status = AlertStatus.Triggered,
            TriggeredAt = DateTimeOffset.UtcNow,
            RuleId = rule.Id
        });
    }
}
