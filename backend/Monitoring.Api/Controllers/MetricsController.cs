using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/servers/{serverId:guid}/[controller]")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly MonitoringDbContext _db;

    public MetricsController(IMetricsService metricsService, MonitoringDbContext db)
    {
        _metricsService = metricsService;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ServerMetricsSummaryDto>> GetServerMetrics(
        Guid serverId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var server = await _db.Servers.FindAsync(serverId);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        var summary = await _metricsService.GetServerMetricsAsync(serverId, from, to);
        return Ok(summary);
    }

    [HttpGet("raw")]
    public async Task<ActionResult<List<ServerMetric>>> GetRawMetrics(
        Guid serverId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int limit = 1000)
    {
        var server = await _db.Servers.FindAsync(serverId);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        var metrics = await _metricsService.GetRawMetricsAsync(serverId, from, to, limit);
        return Ok(metrics);
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }
}
