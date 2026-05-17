using Microsoft.EntityFrameworkCore;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Services;

public class AgentUpdateSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentUpdateSchedulerService> _logger;
    private readonly TimeSpan _interval;

    public AgentUpdateSchedulerService(IServiceScopeFactory scopeFactory, ILogger<AgentUpdateSchedulerService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, config.GetValue<int?>("AgentUpdates:AutoAssignIntervalMinutes") ?? 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await AssignLatestReleasesAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Agent update auto-assignment failed"); }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task AssignLatestReleasesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
        var workspaces = await db.AgentUpdateReleases.Select(r => r.WorkspaceId).Distinct().ToListAsync(ct);
        foreach (var workspaceId in workspaces)
        {
            var latest = await db.AgentUpdateReleases
                .Where(r => r.WorkspaceId == workspaceId && r.IsActive)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (latest is null) continue;

            await ExpireStaleAssignmentsAsync(db, ct);

            var servers = await db.Servers
                .Where(s => s.WorkspaceId == workspaceId && s.ServerToken != null && s.AgentBuildId != latest.BuildId)
                .ToListAsync(ct);

            foreach (var server in servers)
            {
                var now = DateTimeOffset.UtcNow;
                var activeStatuses = new[] { "Pending", "Offered", "Downloading", "Verified", "Restarting" };
                var hasActiveAssignment = await db.AgentUpdateAssignments.AnyAsync(a =>
                    a.ServerId == server.Id && activeStatuses.Contains(a.Status), ct);
                if (hasActiveAssignment)
                {
                    _logger.LogDebug("Skip agent update for server {ServerId}: active assignment exists", server.Id);
                    continue;
                }

                var lastForRelease = await db.AgentUpdateAssignments
                    .Where(a => a.ServerId == server.Id && a.ReleaseId == latest.Id)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (lastForRelease?.NextAttemptAt is not null && lastForRelease.NextAttemptAt > now)
                {
                    _logger.LogInformation("Skip agent update for server {ServerId}: cooldown until {NextAttemptAt}", server.Id, lastForRelease.NextAttemptAt);
                    continue;
                }
                if ((lastForRelease?.RetryCount ?? 0) >= 3)
                {
                    _logger.LogWarning("Skip agent update for server {ServerId}: max retries reached for release {ReleaseId}", server.Id, latest.Id);
                    continue;
                }

                db.AgentUpdateAssignments.Add(new()
                {
                    Id = Guid.NewGuid(),
                    ServerId = server.Id,
                    ReleaseId = latest.Id,
                    FromVersion = server.AgentVersion,
                    FromBuildId = server.AgentBuildId,
                    Status = "Pending",
                    RetryCount = (lastForRelease?.RetryCount ?? 0) + 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                server.AgentUpdateStatus = "Pending";
                _logger.LogInformation("Queued agent update {ReleaseId} for server {ServerId}", latest.Id, server.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task ExpireStaleAssignmentsAsync(MonitoringDbContext db, CancellationToken ct)
    {
        var activeStatuses = new[] { "Pending", "Offered", "Downloading", "Verified", "Restarting" };
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
        var stale = await db.AgentUpdateAssignments
            .Where(a => activeStatuses.Contains(a.Status) && a.UpdatedAt < cutoff)
            .ToListAsync(ct);
        foreach (var assignment in stale)
        {
            assignment.Status = "Failed";
            assignment.ErrorMessage = "Timed out waiting for agent update progress.";
            assignment.LastFailureCode = "timeout";
            assignment.CompletedAt = DateTimeOffset.UtcNow;
            assignment.UpdatedAt = DateTimeOffset.UtcNow;
            assignment.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(30);
            _logger.LogWarning("Expired stale agent update assignment {AssignmentId}", assignment.Id);
        }
    }
}
