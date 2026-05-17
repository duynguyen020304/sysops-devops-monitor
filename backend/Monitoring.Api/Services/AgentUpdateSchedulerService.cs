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

            var servers = await db.Servers
                .Where(s => s.WorkspaceId == workspaceId && s.ServerToken != null && s.AgentBuildId != latest.BuildId)
                .ToListAsync(ct);

            foreach (var server in servers)
            {
                var hasActiveAssignment = await db.AgentUpdateAssignments.AnyAsync(a =>
                    a.ServerId == server.Id &&
                    (a.Status == "Pending" || a.Status == "Offered" || a.Status == "Downloading" || a.Status == "Verified" || a.Status == "Restarting"), ct);
                if (hasActiveAssignment) continue;

                var now = DateTimeOffset.UtcNow;
                db.AgentUpdateAssignments.Add(new()
                {
                    Id = Guid.NewGuid(),
                    ServerId = server.Id,
                    ReleaseId = latest.Id,
                    FromVersion = server.AgentVersion,
                    FromBuildId = server.AgentBuildId,
                    Status = "Pending",
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                server.AgentUpdateStatus = "Pending";
                _logger.LogInformation("Queued agent update {ReleaseId} for server {ServerId}", latest.Id, server.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
