using Microsoft.EntityFrameworkCore;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class LogService : ILogService
{
    private readonly MonitoringDbContext _db;
    private readonly ILogMaskingService _logMaskingService;

    public LogService(MonitoringDbContext db, ILogMaskingService logMaskingService)
    {
        _db = db;
        _logMaskingService = logMaskingService;
    }

    public async Task<LogSearchResult> SearchLogsAsync(Guid workspaceId, LogSearchRequest request)
    {
        var items = new List<LogEntryDto>();

        // Search workflow logs if no source filter or source = "workflow"
        if (string.IsNullOrEmpty(request.SourceType) ||
            request.SourceType.Equals("workflow", StringComparison.OrdinalIgnoreCase))
        {
            var workflowLogs = await SearchWorkflowLogsAsync(workspaceId, request);
            items.AddRange(workflowLogs);
        }

        // Search PM2 logs if no source filter or source = "pm2"
        if (string.IsNullOrEmpty(request.SourceType) ||
            request.SourceType.Equals("pm2", StringComparison.OrdinalIgnoreCase))
        {
            var pm2Logs = await SearchPM2LogsAsync(workspaceId, request);
            items.AddRange(pm2Logs);
        }

        // Sort combined results by timestamp descending
        items = items
            .OrderByDescending(i => i.Timestamp)
            .ToList();

        var total = items.Count;

        // Apply pagination on the combined results
        items = items
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new LogSearchResult(items, total, request.Page, request.PageSize);
    }

    public async Task<List<WorkflowLog>> GetWorkflowLogsAsync(Guid workflowRunId, int limit = 100)
    {
        return await _db.WorkflowLogs
            .Where(l => l.WorkflowRunId == workflowRunId)
            .OrderBy(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<PM2Log>> GetPM2LogsAsync(Guid processId, int limit = 100)
    {
        return await _db.PM2Logs
            .Where(l => l.ProcessId == processId)
            .OrderBy(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    private async Task<List<LogEntryDto>> SearchWorkflowLogsAsync(
        Guid workspaceId, LogSearchRequest request)
    {
        // Join through WorkflowRun -> Repository to filter by workspace
        var query = from log in _db.WorkflowLogs
                    join run in _db.WorkflowRuns on log.WorkflowRunId equals run.Id
                    join repo in _db.Repositories on run.RepositoryId equals repo.Id
                    where repo.WorkspaceId == workspaceId
                    select new { log, run };

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var keyword = request.Keyword;
            query = query.Where(x => x.log.Message.Contains(keyword));
        }

        if (request.From.HasValue)
            query = query.Where(x => x.log.Timestamp >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(x => x.log.Timestamp <= request.To.Value);

        if (!string.IsNullOrEmpty(request.Severity))
        {
            var severity = request.Severity;
            query = query.Where(x => x.log.Level == severity);
        }

        var results = await query
            .OrderByDescending(x => x.log.Timestamp)
            .Select(x => new LogEntryDto(
                x.log.Id,
                "workflow",
                x.run.WorkflowName,
                x.log.Timestamp,
                x.log.Level,
                _logMaskingService.MaskSensitiveData(x.log.Message)
            ))
            .ToListAsync();

        return results;
    }

    private async Task<List<LogEntryDto>> SearchPM2LogsAsync(
        Guid workspaceId, LogSearchRequest request)
    {
        // Join through Server to filter by workspace
        var query = from log in _db.PM2Logs
                    join server in _db.Servers on log.ServerId equals server.Id
                    where server.WorkspaceId == workspaceId
                    select new { log, server };

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var keyword = request.Keyword;
            query = query.Where(x => x.log.Message.Contains(keyword));
        }

        if (request.From.HasValue)
            query = query.Where(x => x.log.Timestamp >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(x => x.log.Timestamp <= request.To.Value);

        if (!string.IsNullOrEmpty(request.Severity))
        {
            var severity = request.Severity;
            query = query.Where(x => x.log.Level == severity);
        }

        var results = await query
            .OrderByDescending(x => x.log.Timestamp)
            .Select(x => new LogEntryDto(
                x.log.Id,
                "pm2",
                x.server.Hostname,
                x.log.Timestamp,
                x.log.Level,
                _logMaskingService.MaskSensitiveData(x.log.Message)
            ))
            .ToListAsync();

        return results;
    }
}
