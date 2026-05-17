using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Interfaces;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId:guid}/[controller]")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly IGitHubService _gitHubService;

    public WorkflowsController(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    [HttpGet]
    [RequirePermission("view_servers")]
    public async Task<ActionResult<PagedResult<WorkflowRunDto>>> GetWorkflowRuns(
        Guid repositoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var repository = await _gitHubService.GetRepositoryAsync(repositoryId);
        if (repository is null)
            return NotFound();

        await EnsureSameWorkspace(repository.WorkspaceId);

        // Sync latest runs from GitHub, returns all synced runs
        var syncedRuns = await _gitHubService.SyncWorkflowRunsAsync(repositoryId);

        var total = syncedRuns.Count;
        var pagedRuns = syncedRuns
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new WorkflowRunDto(
                Id: r.Id,
                WorkflowName: r.WorkflowName,
                GithubRunId: r.GithubRunId,
                Branch: r.Branch,
                CommitSha: r.CommitSha,
                CommitMessage: r.CommitMessage,
                Actor: r.Actor,
                EventType: r.EventType,
                Status: r.Status,
                Conclusion: r.Conclusion,
                StartedAt: r.StartedAt,
                CompletedAt: r.CompletedAt,
                DurationSeconds: r.DurationSeconds,
                HtmlUrl: r.HtmlUrl
            ))
            .ToList();

        return Ok(new PagedResult<WorkflowRunDto>(pagedRuns, total, page, pageSize));
    }

    [HttpGet("{runId:long}")]
    [RequirePermission("view_servers")]
    public async Task<ActionResult<WorkflowRunDetailDto>> GetWorkflowRunDetail(
        Guid repositoryId, long runId)
    {
        var repository = await _gitHubService.GetRepositoryAsync(repositoryId);
        if (repository is null)
            return NotFound();

        await EnsureSameWorkspace(repository.WorkspaceId);

        // Sync to ensure run exists, then find it
        var syncedRuns = await _gitHubService.SyncWorkflowRunsAsync(repositoryId);
        var run = syncedRuns.FirstOrDefault(r => r.GithubRunId == runId);
        if (run is null)
            return NotFound();

        var logPage = await _gitHubService.GetWorkflowLogPageAsync(repositoryId, runId, cursor: null, limit: 100);

        var runDto = new WorkflowRunDto(
            Id: run.Id,
            WorkflowName: run.WorkflowName,
            GithubRunId: run.GithubRunId,
            Branch: run.Branch,
            CommitSha: run.CommitSha,
            CommitMessage: run.CommitMessage,
            Actor: run.Actor,
            EventType: run.EventType,
            Status: run.Status,
            Conclusion: run.Conclusion,
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            DurationSeconds: run.DurationSeconds,
            HtmlUrl: run.HtmlUrl
        );

        return Ok(new WorkflowRunDetailDto(runDto, logPage));
    }

    [HttpGet("{runId:long}/logs")]
    [RequirePermission("view_servers")]
    public async Task<ActionResult<WorkflowLogPageDto>> GetWorkflowLogs(
        Guid repositoryId,
        long runId,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 200,
        [FromQuery] int? pageSize = null)
    {
        var repository = await _gitHubService.GetRepositoryAsync(repositoryId);
        if (repository is null)
            return NotFound();

        await EnsureSameWorkspace(repository.WorkspaceId);

        var effectiveLimit = pageSize.HasValue && pageSize.Value > 0 ? pageSize.Value : limit;
        var logPage = await _gitHubService.GetWorkflowLogPageAsync(repositoryId, runId, cursor, effectiveLimit);
        return Ok(logPage);
    }

    [HttpGet("~/api/repositories/{repositoryId:guid}/stats")]
    [RequirePermission("view_servers")]
    public async Task<ActionResult<RepositoryStatsDto>> GetRepositoryStats(Guid repositoryId)
    {
        var repository = await _gitHubService.GetRepositoryAsync(repositoryId);
        if (repository is null)
            return NotFound();

        await EnsureSameWorkspace(repository.WorkspaceId);

        var stats = await _gitHubService.GetRepositoryStatsAsync(repositoryId);
        return Ok(stats);
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }

    private async Task EnsureSameWorkspace(Guid repositoryWorkspaceId)
    {
        var workspaceId = GetWorkspaceId();
        if (repositoryWorkspaceId != workspaceId)
            throw new UnauthorizedAccessException("Repository does not belong to your workspace.");
    }
}
