using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Interfaces;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;

    public LogsController(ILogService logService)
    {
        _logService = logService;
    }

    [HttpGet("search")]
    [RequirePermission("view_audit_logs")]
    public async Task<ActionResult<LogSearchResult>> SearchLogs(
        [FromQuery] string? keyword,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? sourceType,
        [FromQuery] string? severity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var workspaceId = GetWorkspaceId();

        var request = new LogSearchRequest(
            Keyword: keyword,
            From: from,
            To: to,
            SourceType: sourceType,
            Severity: severity,
            Page: page,
            PageSize: pageSize
        );

        var result = await _logService.SearchLogsAsync(workspaceId, request);
        return Ok(result);
    }

    [HttpGet("stream")]
    [RequirePermission("view_audit_logs")]
    public IActionResult StreamLogs()
    {
        // Placeholder for future SSE/SignalR real-time log streaming
        return Ok(new { message = "Log streaming is not yet implemented. This endpoint is reserved for future SSE/SignalR support." });
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }
}
