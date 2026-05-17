using Monitoring.Core.Entities;

namespace Monitoring.Core.Interfaces;

public interface IWorkflowLogCache
{
    Task<IReadOnlyList<WorkflowLog>?> GetAsync(Guid repositoryId, long githubRunId, CancellationToken cancellationToken = default);
    Task SetAsync(Guid repositoryId, long githubRunId, IReadOnlyList<WorkflowLog> logs, CancellationToken cancellationToken = default);
}
