using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;

namespace Monitoring.Core.Interfaces;

public interface IGitHubService
{
    Task<Repository> ConnectRepositoryAsync(Guid workspaceId, string githubToken, string owner, string name);
    Task<List<Repository>> GetRepositoriesAsync(Guid workspaceId);
    Task<Repository?> GetRepositoryAsync(Guid repositoryId);
    Task DisconnectRepositoryAsync(Guid repositoryId);
    Task<List<WorkflowRun>> SyncWorkflowRunsAsync(Guid repositoryId);
    Task<List<WorkflowLog>> GetWorkflowLogsAsync(Guid repositoryId, long githubRunId);
    Task<WorkflowLogPageDto> GetWorkflowLogPageAsync(Guid repositoryId, long githubRunId, string? cursor, int limit);
    Task<RepositoryStatsDto> GetRepositoryStatsAsync(Guid repositoryId);
}
