using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/pm2")]
[Authorize]
[RequirePermission("view_pm2_logs")]
public class PM2Controller : ControllerBase
{
    private const int MaxLogLimit = 1000;

    private readonly IPM2Service _pm2Service;
    private readonly IServerService _serverService;

    public PM2Controller(IPM2Service pm2Service, IServerService serverService)
    {
        _pm2Service = pm2Service;
        _serverService = serverService;
    }

    // Backward-compatible endpoint used by frontend: /api/servers/{serverId}/pm2
    [HttpGet("~/api/servers/{serverId:guid}/pm2")]
    public async Task<ActionResult<List<PM2ProcessDetailDto>>> GetProcessesByServer(Guid serverId)
    {
        var server = await GetServerInWorkspaceAsync(serverId);
        if (server is null) return NotFound();

        var processes = await _pm2Service.GetProcessesByServerAsync(serverId);
        return Ok(processes.Select(MapProcess).ToList());
    }

    [HttpGet("{processId:guid}")]
    public async Task<ActionResult<PM2ProcessDetailDto>> GetProcess(Guid processId)
    {
        var process = await _pm2Service.GetProcessAsync(processId);
        if (process is null) return NotFound();

        var server = await GetServerInWorkspaceAsync(process.ServerId);
        if (server is null) return NotFound();

        return Ok(MapProcess(process));
    }

    [HttpGet("{processId:guid}/logs")]
    public async Task<ActionResult<List<PM2LogDto>>> GetProcessLogs(
        Guid processId,
        [FromQuery] int limit = 100)
    {
        var process = await _pm2Service.GetProcessAsync(processId);
        if (process is null) return NotFound();

        var server = await GetServerInWorkspaceAsync(process.ServerId);
        if (server is null) return NotFound();

        var safeLimit = Math.Clamp(limit, 1, MaxLogLimit);
        var logs = await _pm2Service.GetProcessLogsAsync(processId, safeLimit);
        return Ok(logs.Select(MapLog).ToList());
    }

    private async Task<Server?> GetServerInWorkspaceAsync(Guid serverId)
    {
        var server = await _serverService.GetServerAsync(serverId);
        if (server is null) return null;

        return server.WorkspaceId == GetWorkspaceId() ? server : null;
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }

    private static PM2ProcessDetailDto MapProcess(PM2Process process)
    {
        return new PM2ProcessDetailDto(
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
    }

    private static PM2LogDto MapLog(PM2Log log)
    {
        return new PM2LogDto(
            Id: log.Id,
            StreamType: log.StreamType == LogStreamType.StdErr ? "stderr" : "stdout",
            Timestamp: log.Timestamp,
            Level: log.Level,
            Message: log.Message
        );
    }
}
