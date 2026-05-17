namespace Monitoring.Core.Interfaces;

public interface IAgentCleanupService
{
    Task<int> ArchiveStaleTokensAsync(Guid workspaceId, string? serverName, string reason, Guid? keepId = null, CancellationToken ct = default);
    Task<int> ArchiveStaleServerDuplicatesAsync(Guid workspaceId, Guid keepId, string hostname, string? machineId, string reason, CancellationToken ct = default);
    Task<int> ArchiveStaleServerDuplicatesAsync(Guid workspaceId, string reason, TimeSpan staleThreshold, CancellationToken ct = default);
    Task<int> ArchiveStaleTokensAsync(string reason, TimeSpan maxAge, CancellationToken ct = default);
    Task<AgentCleanupResult> CleanupStaleAsync(string reason, TimeSpan tokenMaxAge, TimeSpan serverStaleThreshold, CancellationToken ct = default);
}

public sealed record AgentCleanupResult(int TokensArchived, int ServersArchived);
