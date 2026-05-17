using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;

namespace Monitoring.Infrastructure.Services;

public sealed class NullWorkflowLogCache : IWorkflowLogCache
{
    public Task<IReadOnlyList<WorkflowLog>?> GetAsync(Guid repositoryId, long githubRunId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkflowLog>?>(null);

    public Task SetAsync(Guid repositoryId, long githubRunId, IReadOnlyList<WorkflowLog> logs, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
