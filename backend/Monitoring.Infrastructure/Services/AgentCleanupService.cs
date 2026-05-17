using Microsoft.EntityFrameworkCore;
using Monitoring.Core.Interfaces;
using Monitoring.Core.Enums;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public sealed class AgentCleanupService : IAgentCleanupService
{
    private readonly MonitoringDbContext _db;

    public AgentCleanupService(MonitoringDbContext db) => _db = db;

    public async Task<int> ArchiveStaleTokensAsync(Guid workspaceId, string? serverName, string reason, Guid? keepId = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.AgentInstallTokens.Where(t => t.WorkspaceId == workspaceId && t.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(serverName)) query = query.Where(t => t.ServerName == serverName);
        var tokens = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        var archived = ArchiveTokenGroups(tokens, now, reason, keepId);
        if (archived > 0) await _db.SaveChangesAsync(ct);
        return archived;
    }

    public async Task<int> ArchiveStaleTokensAsync(string reason, TimeSpan maxAge, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - maxAge;
        var tokens = await _db.AgentInstallTokens
            .Where(t => t.ArchivedAt == null && (t.UsedAt != null || t.RevokedAt != null || t.ExpiresAt < now || t.CreatedAt < cutoff))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var archived = ArchiveTokenGroups(tokens, now, reason);
        if (archived > 0) await _db.SaveChangesAsync(ct);
        return archived;
    }

    public async Task<int> ArchiveStaleServerDuplicatesAsync(Guid workspaceId, Guid keepId, string hostname, string? machineId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var staleBefore = DateTimeOffset.UtcNow.AddMinutes(-2);
        var candidates = await _db.Servers
            .Where(s => s.WorkspaceId == workspaceId && s.Id != keepId && s.ArchivedAt == null &&
                ((!string.IsNullOrWhiteSpace(machineId) && s.MachineId == machineId) || s.Hostname == hostname))
            .ToListAsync(ct);
        var archived = ArchiveServers(candidates, now, staleBefore, reason);
        if (archived > 0) await _db.SaveChangesAsync(ct);
        return archived;
    }

    public async Task<int> ArchiveStaleServerDuplicatesAsync(Guid workspaceId, string reason, TimeSpan staleThreshold, CancellationToken ct = default)
    {
        var servers = await _db.Servers
            .Where(s => s.WorkspaceId == workspaceId && s.ArchivedAt == null)
            .OrderByDescending(s => s.LastHeartbeatAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        var staleBefore = DateTimeOffset.UtcNow.Subtract(staleThreshold);
        var archived = 0;
        foreach (var group in servers.GroupBy(s => !string.IsNullOrWhiteSpace(s.MachineId) ? $"m:{s.MachineId}" : $"h:{s.Hostname}"))
        {
            foreach (var server in group.Skip(1))
            {
                if (IsFreshHealthy(server, staleBefore)) continue;
                server.ArchivedAt = now;
                server.ArchiveReason = reason;
                archived++;
            }
        }
        if (archived > 0) await _db.SaveChangesAsync(ct);
        return archived;
    }

    public async Task<AgentCleanupResult> CleanupStaleAsync(string reason, TimeSpan tokenMaxAge, TimeSpan serverStaleThreshold, CancellationToken ct = default)
    {
        var tokensArchived = await ArchiveStaleTokensAsync(reason, tokenMaxAge, ct);
        var workspaceIds = await _db.Servers.Where(s => s.ArchivedAt == null).Select(s => s.WorkspaceId).Distinct().ToListAsync(ct);
        var serversArchived = 0;
        foreach (var workspaceId in workspaceIds)
            serversArchived += await ArchiveStaleServerDuplicatesAsync(workspaceId, reason, serverStaleThreshold, ct);
        return new AgentCleanupResult(tokensArchived, serversArchived);
    }

    private static int ArchiveTokenGroups(IEnumerable<Core.Entities.AgentInstallToken> tokens, DateTime now, string reason, Guid? keepId = null)
    {
        var archived = 0;
        foreach (var group in tokens.GroupBy(t => new { t.WorkspaceId, t.ServerName }))
        {
            var keep = keepId.HasValue ? group.FirstOrDefault(t => t.Id == keepId.Value) : group.FirstOrDefault(t => t.RevokedAt == null && t.UsedAt == null && t.ExpiresAt >= now);
            keep ??= group.OrderByDescending(t => t.CreatedAt).FirstOrDefault();
            foreach (var token in group)
            {
                if (token.Id == keep?.Id) continue;
                if (token.UsedAt is null && token.RevokedAt is null && token.ExpiresAt >= now) continue;
                token.ArchivedAt = now;
                token.ArchiveReason = reason;
                archived++;
            }
        }
        return archived;
    }

    private static int ArchiveServers(IEnumerable<Core.Entities.Server> servers, DateTime now, DateTimeOffset staleBefore, string reason)
    {
        var archived = 0;
        foreach (var server in servers)
        {
            if (IsFreshHealthy(server, staleBefore)) continue;
            server.ArchivedAt = now;
            server.ArchiveReason = reason;
            archived++;
        }
        return archived;
    }

    private static bool IsFreshHealthy(Core.Entities.Server server, DateTimeOffset staleBefore) =>
        server.LastHeartbeatAt is not null && server.LastHeartbeatAt > staleBefore && server.Status == ServerStatus.Healthy;
}
