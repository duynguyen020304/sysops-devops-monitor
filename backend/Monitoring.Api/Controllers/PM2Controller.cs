using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Core.DTOs;
using Monitoring.Core.Interfaces;
using System.Security.Claims;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class PM2Controller : ControllerBase
{
    private readonly IPM2Service _pm2Service;
    private readonly IServerService _serverService;

    public PM2Controller(IPM2Service pm2Service, IServerService serverService)
    {
        _pm2Service = pm2Service;
        _serverService = serverService;
    }

    [HttpGet("servers/{id:guid}/pm2")]
    public async Task<ActionResult<List<PM2ProcessDetailDto>>> GetProcesses(Guid id)
    {
        var server = await _serverService.GetServerAsync(id);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        var processes = await _pm2Service.GetProcessesByServerAsync(id);

        var dtos = processes.Select(p => new PM2ProcessDetailDto(
            Id: p.Id,
            Pm2Id: p.Pm2Id,
            Name: p.Name,
            Pid: p.Pid,
            Status: p.Status.ToString(),
            UptimeSeconds: p.UptimeSeconds,
            RestartCount: p.RestartCount,
            CpuUsage: p.CpuUsage,
            MemoryUsage: p.MemoryUsage,
            ExecutionMode: p.ExecutionMode,
            NodeVersion: p.NodeVersion,
            CreatedAt: p.CreatedAt
        )).ToList();

        return Ok(dtos);
    }

    [HttpGet("pm2/{processId:guid}")]
    public async Task<ActionResult<PM2ProcessDetailDto>> GetProcess(Guid processId)
    {
        var process = await _pm2Service.GetProcessAsync(processId);
        if (process is null)
            return NotFound();

        // Verify the server belongs to the user's workspace
        var server = await _serverService.GetServerAsync(process.ServerId);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        var dto = new PM2ProcessDetailDto(
            Id: process.Id,
            Pm2Id: process.Pm2Id,
            Name: process.Name,
            Pid: process.Pid,
            Status: process.Status.ToString(),
            UptimeSeconds: process.UptimeSeconds,
            RestartCount: process.RestartCount,
            CpuUsage: process.CpuUsage,
            MemoryUsage: process.MemoryUsage,
            ExecutionMode: process.ExecutionMode,
            NodeVersion: process.NodeVersion,
            CreatedAt: process.CreatedAt
        );

        return Ok(dto);
    }

    [HttpGet("pm2/{processId:guid}/logs")]
    public async Task<ActionResult<List<PM2LogDto>>> GetProcessLogs(
        Guid processId,
        [FromQuery] int limit = 100)
    {
        var process = await _pm2Service.GetProcessAsync(processId);
        if (process is null)
            return NotFound();

        // Verify the server belongs to the user's workspace
        var server = await _serverService.GetServerAsync(process.ServerId);
        if (server is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (server.WorkspaceId != workspaceId)
            return Forbid();

        var logs = await _pm2Service.GetProcessLogsAsync(processId, limit);

        var dtos = logs.Select(l => new PM2LogDto(
            Id: l.Id,
            StreamType: l.StreamType.ToString(),
            Timestamp: l.Timestamp,
            Level: l.Level,
            Message: l.Message
        )).ToList();

        return Ok(dtos);
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }
}
