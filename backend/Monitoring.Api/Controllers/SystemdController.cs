using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/systemd")]
[RequirePermission("view_pm2_logs")]
public class SystemdController : ControllerBase
{
    private const int MaxLogLimit = 1000;
    private readonly MonitoringDbContext _db;

    public SystemdController(MonitoringDbContext db) => _db = db;

    [HttpPost("~/api/servers/{serverId:guid}/systemd/refresh")]
    [RequirePermission("connect_agents")]
    public async Task<ActionResult<SystemdRefreshResponse>> RequestRefresh(Guid serverId)
    {
        if (!await ServerInWorkspace(serverId)) return NotFound();
        var server = await _db.Servers.FindAsync(serverId);
        if (server is null) return NotFound();
        var now = DateTimeOffset.UtcNow;
        server.SystemdRefreshRequestedAt = now;
        server.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new SystemdRefreshResponse(true, now));
    }

    [HttpGet("~/api/servers/{serverId:guid}/systemd")]
    public async Task<ActionResult<List<SystemdServiceDto>>> GetByServer(Guid serverId, [FromQuery] string? search, [FromQuery] string? state, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        if (!await ServerInWorkspace(serverId)) return NotFound();
        var query = _db.SystemdServices.Where(s => s.ServerId == serverId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(s => s.Name.Contains(search) || (s.Description != null && s.Description.Contains(search)));
        if (!string.IsNullOrWhiteSpace(state)) query = query.Where(s => s.ActiveState == state);
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 500);
        var rows = await query.OrderBy(s => s.Name).Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync();
        return Ok(rows.Select(MapService).ToList());
    }

    [HttpGet("{serviceId:guid}")]
    public async Task<ActionResult<SystemdServiceDto>> Get(Guid serviceId)
    {
        var svc = await _db.SystemdServices.FindAsync(serviceId);
        if (svc is null || !await ServerInWorkspace(svc.ServerId)) return NotFound();
        return Ok(MapService(svc));
    }

    [HttpGet("{serviceId:guid}/logs")]
    public async Task<ActionResult<List<SystemdLogDto>>> GetLogs(Guid serviceId, [FromQuery] int limit = 100, [FromQuery] string? level = null, [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null)
    {
        var svc = await _db.SystemdServices.FindAsync(serviceId);
        if (svc is null || !await ServerInWorkspace(svc.ServerId)) return NotFound();
        var query = _db.SystemdLogs.Where(l => l.ServiceId == serviceId);
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(l => l.Level == level);
        if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(l => l.Timestamp <= to.Value);
        var rows = await query.OrderByDescending(l => l.Timestamp).Take(Math.Clamp(limit, 1, MaxLogLimit)).ToListAsync();
        return Ok(rows.Select(l => new SystemdLogDto(l.Id, l.Timestamp, l.Priority, l.Level, l.Message, l.Cursor, l.BootId)).ToList());
    }

    private async Task<bool> ServerInWorkspace(Guid serverId)
    {
        var workspaceId = Guid.Parse(User.FindFirst("WorkspaceId")?.Value ?? throw new UnauthorizedAccessException("WorkspaceId claim not found."));
        return await _db.Servers.AnyAsync(s => s.Id == serverId && s.WorkspaceId == workspaceId);
    }

    private static SystemdServiceDto MapService(SystemdService s) => new(s.Id, s.ServerId, s.Name, s.DisplayName, s.LoadState, s.ActiveState, s.SubState, s.Description, s.FragmentPath, s.MainPid, s.MemoryCurrent, s.CpuUsageNSec, s.RestartCount, s.UpdatedAt);
}
