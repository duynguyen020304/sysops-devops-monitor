using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;
using System.Security.Claims;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServersController : ControllerBase
{
    private readonly IServerService _serverService;
    private readonly MonitoringDbContext _db;

    public ServersController(IServerService serverService, MonitoringDbContext db)
    {
        _serverService = serverService;
        _db = db;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ServerDto>> Register([FromBody] RegisterServerRequest request)
    {
        // Agent registration uses a default workspace; in production this would
        // be tied to the agent token's associated workspace.
        var workspaceId = GetWorkspaceId();

        var server = await _serverService.RegisterServerAsync(workspaceId, request);

        return CreatedAtAction(nameof(GetServer), new { id = server.Id }, MapToDto(server));
    }

    [HttpGet]
    public async Task<ActionResult<List<ServerDto>>> GetServers()
    {
        var workspaceId = GetWorkspaceId();
        var servers = await _serverService.GetServersAsync(workspaceId);

        return Ok(servers.Select(MapToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ServerDto>> GetServer(Guid id)
    {
        var server = await _serverService.GetServerAsync(id);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        return Ok(MapToDto(server));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateServer(Guid id, [FromBody] RegisterServerRequest request)
    {
        var server = await _serverService.GetServerAsync(id);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        await _serverService.UpdateServerAsync(id, request);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteServer(Guid id)
    {
        var server = await _serverService.GetServerAsync(id);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        await _serverService.DeleteServerAsync(id);
        return NoContent();
    }

    [HttpGet("{id:guid}/metrics")]
    public async Task<ActionResult<List<ServerMetric>>> GetMetrics(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var server = await _serverService.GetServerAsync(id);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        var query = _db.ServerMetrics.Where(m => m.ServerId == id);

        if (from.HasValue)
            query = query.Where(m => m.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.Timestamp <= to.Value);

        var metrics = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(500)
            .ToListAsync();

        return Ok(metrics);
    }

    [HttpGet("{id:guid}/health")]
    public async Task<ActionResult<ServerHealthDto>> GetHealth(Guid id)
    {
        var server = await _serverService.GetServerAsync(id);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        var health = await _serverService.GetServerHealthAsync(id);
        return Ok(health);
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }

    private static ServerDto MapToDto(Core.Entities.Server server)
    {
        return new ServerDto(
            Id: server.Id,
            Hostname: server.Hostname,
            IpAddress: server.IpAddress,
            OperatingSystem: server.OperatingSystem,
            AgentVersion: server.AgentVersion,
            Status: server.Status.ToString(),
            LastHeartbeatAt: server.LastHeartbeatAt,
            CreatedAt: server.CreatedAt
        );
    }
}
