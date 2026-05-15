using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;

namespace Monitoring.Core.Interfaces;

public interface ILogService
{
    Task<LogSearchResult> SearchLogsAsync(Guid workspaceId, LogSearchRequest request);
    Task<List<WorkflowLog>> GetWorkflowLogsAsync(Guid workflowRunId, int limit = 100);
    Task<List<PM2Log>> GetPM2LogsAsync(Guid processId, int limit = 100);
}
