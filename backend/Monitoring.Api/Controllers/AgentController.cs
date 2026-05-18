using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IServerService _serverService;
    private readonly IPM2Service _pm2Service;
    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;

    public AgentController(
        IServerService serverService,
        IPM2Service pm2Service,
        MonitoringDbContext db,
        IConfiguration config)
    {
        _serverService = serverService;
        _pm2Service = pm2Service;
        _db = db;
        _config = config;
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] AgentHeartbeatRequest request)
    {
        if (!await ValidateAgentTokenAsync())
            return Unauthorized(new { message = "Invalid agent token." });

        try
        {
            await _serverService.ProcessHeartbeatAsync(request.ServerId);
            var server = await _db.Servers.FindAsync(request.ServerId);
            if (server is not null)
            {
                if (!string.IsNullOrWhiteSpace(request.AgentVersion)) server.AgentVersion = request.AgentVersion;
                if (!string.IsNullOrWhiteSpace(request.BuildId)) server.AgentBuildId = request.BuildId;
                if (request.Capabilities is not null) server.AgentCapabilitiesJson = JsonSerializer.Serialize(request.Capabilities);
                if (!string.IsNullOrWhiteSpace(request.UpdateState)) server.AgentUpdateStatus = request.UpdateState;
                server.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            return Ok(new { message = "Heartbeat received." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("systemd/refresh-command")]
    public async Task<IActionResult> GetSystemdRefreshCommand()
    {
        if (!await ValidateAgentTokenAsync()) return Unauthorized(new { message = "Invalid agent token." });
        if (!Guid.TryParse(Request.Headers["X-Server-Id"].FirstOrDefault(), out var serverId)) return BadRequest(new { message = "Missing server id." });
        var server = await _db.Servers.FindAsync(serverId);
        if (server is null) return NotFound(new { message = "Server not found." });
        var due = server.SystemdRefreshRequestedAt.HasValue && (!server.SystemdLastRefreshedAt.HasValue || server.SystemdRefreshRequestedAt > server.SystemdLastRefreshedAt);
        return Ok(new { refresh = due, requestedAt = server.SystemdRefreshRequestedAt });
    }

    [HttpPost("metrics")]
    public async Task<IActionResult> SubmitMetrics([FromBody] AgentMetricsRequest request)
    {
        if (!await ValidateAgentTokenAsync())
            return Unauthorized(new { message = "Invalid agent token." });

        try
        {
            await _serverService.ProcessMetricsAsync(request.ServerId, request.Metrics);
            return Ok(new { message = "Metrics recorded." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("pm2")]
    public async Task<IActionResult> SubmitPM2Data([FromBody] AgentPM2Request request)
    {
        if (!await ValidateAgentTokenAsync())
            return Unauthorized(new { message = "Invalid agent token." });

        try
        {
            await _pm2Service.ProcessPM2DataAsync(request.ServerId, request.Processes);
            return Ok(new { message = "PM2 data recorded." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("systemd")]
    public async Task<IActionResult> SubmitSystemd([FromBody] AgentSystemdRequest request)
    {
        if (!await ValidateAgentTokenAsync()) return Unauthorized(new { message = "Invalid agent token." });
        if (await _serverService.GetServerAsync(request.ServerId) is null) return NotFound(new { message = "Server not found." });

        var now = DateTime.UtcNow;
        var existing = await _db.SystemdServices.Where(s => s.ServerId == request.ServerId).ToDictionaryAsync(s => s.Name);
        foreach (var dto in request.Services.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
        {
            if (!existing.TryGetValue(dto.Name, out var svc))
            {
                svc = new SystemdService { Id = Guid.NewGuid(), ServerId = request.ServerId, Name = dto.Name, LoadState = dto.LoadState, ActiveState = dto.ActiveState, SubState = dto.SubState, CreatedAt = now, UpdatedAt = now };
                _db.SystemdServices.Add(svc);
            }
            svc.DisplayName = dto.DisplayName;
            svc.LoadState = dto.LoadState;
            svc.ActiveState = dto.ActiveState;
            svc.SubState = dto.SubState;
            svc.Description = dto.Description;
            svc.FragmentPath = dto.FragmentPath;
            svc.MainPid = dto.MainPid;
            svc.MemoryCurrent = dto.MemoryCurrent;
            svc.CpuUsageNSec = dto.CpuUsageNSec;
            svc.RestartCount = dto.RestartCount;
            svc.UpdatedAt = now;
        }
        var server = await _db.Servers.FindAsync(request.ServerId);
        if (server is not null)
        {
            server.SystemdLastRefreshedAt = DateTimeOffset.UtcNow;
            server.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return Ok(new { received = request.Services.Count });
    }

    [HttpPost("systemd/logs")]
    public async Task<IActionResult> SubmitSystemdLogs([FromBody] AgentSystemdLogsRequest request)
    {
        if (!await ValidateAgentTokenAsync()) return Unauthorized(new { message = "Invalid agent token." });
        if (await _serverService.GetServerAsync(request.ServerId) is null) return NotFound(new { message = "Server not found." });

        var services = await _db.SystemdServices.Where(s => s.ServerId == request.ServerId).ToDictionaryAsync(s => s.Name, s => s.Id);
        var candidates = new List<SystemdLog>();
        var droppedNoService = 0;
        var now = DateTime.UtcNow;
        foreach (var entry in request.Logs)
        {
            if (!services.TryGetValue(entry.UnitName, out var serviceId)) { droppedNoService++; continue; }
            var ts = entry.Timestamp ?? DateTimeOffset.UtcNow;
            candidates.Add(new SystemdLog
            {
                Id = Guid.NewGuid(), ServerId = request.ServerId, ServiceId = serviceId, Timestamp = ts,
                Priority = entry.Priority, Level = entry.Level ?? MapPriority(entry.Priority), Message = entry.Message,
                RawJson = entry.RawJson ?? entry.Message, Cursor = entry.Cursor, BootId = entry.BootId,
                Fingerprint = CreateSystemdFingerprint(request.ServerId, serviceId, entry.Cursor, ts, entry.Message), CreatedAt = now
            });
        }
        var fps = candidates.Select(l => l.Fingerprint).Distinct().ToList();
        var existingFps = await _db.SystemdLogs.Where(l => fps.Contains(l.Fingerprint)).Select(l => l.Fingerprint).ToListAsync();
        var existingSet = existingFps.ToHashSet(StringComparer.Ordinal);
        var inserted = 0;
        foreach (var log in candidates.GroupBy(l => l.Fingerprint).Select(g => g.First()))
            if (existingSet.Add(log.Fingerprint)) { _db.SystemdLogs.Add(log); inserted++; }
        await _db.SaveChangesAsync();
        return Ok(new { received = request.Logs.Count, inserted, duplicates = request.Logs.Count - inserted - droppedNoService, droppedNoService });
    }

    [HttpPost("logs")]
    public async Task<IActionResult> SubmitLogs([FromBody] AgentLogsRequest request)
    {
        if (!await ValidateAgentTokenAsync())
            return Unauthorized(new { message = "Invalid agent token." });

        // Verify server exists
        var server = await _serverService.GetServerAsync(request.ServerId);
        if (server is null)
            return NotFound(new { message = "Server not found." });

        var now = DateTime.UtcNow;
        var processRows = await _db.PM2Processes
            .Where(p => p.ServerId == request.ServerId)
            .ToListAsync();

        var processIdsByName = processRows
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Id = g.OrderByDescending(p => p.UpdatedAt).First().Id })
            .ToDictionary(p => p.Name, p => p.Id);

        var candidates = new List<PM2Log>();
        var droppedNoProcess = 0;

        foreach (var logEntry in request.Logs)
        {
            var streamType = logEntry.StreamType.ToLowerInvariant() switch
            {
                "stderr" or "err" => LogStreamType.StdErr,
                _ => LogStreamType.StdOut
            };

            var processId = logEntry.ProcessId;
            if (processId is null && !string.IsNullOrWhiteSpace(logEntry.ProcessName)
                                  && processIdsByName.TryGetValue(logEntry.ProcessName, out var mappedProcessId))
            {
                processId = mappedProcessId;
            }

            if (processId is null)
            {
                droppedNoProcess++;
                continue;
            }

            var timestamp = logEntry.Timestamp ?? DateTimeOffset.UtcNow;
            candidates.Add(new PM2Log
            {
                Id = Guid.NewGuid(),
                ServerId = request.ServerId,
                ProcessId = processId.Value,
                StreamType = streamType,
                Timestamp = timestamp,
                Level = logEntry.Level,
                Message = logEntry.Message,
                RawMessage = logEntry.Message,
                Fingerprint = CreateLogFingerprint(request.ServerId, processId.Value, streamType, timestamp, logEntry.Message),
                CreatedAt = now
            });
        }

        var fingerprints = candidates.Select(l => l.Fingerprint).Distinct().ToList();
        var existingFingerprints = await _db.PM2Logs
            .Where(l => fingerprints.Contains(l.Fingerprint))
            .Select(l => l.Fingerprint)
            .ToListAsync();
        var existingSet = existingFingerprints.ToHashSet(StringComparer.Ordinal);
        var inserted = 0;
        foreach (var pm2Log in candidates.GroupBy(l => l.Fingerprint).Select(g => g.First()))
        {
            if (existingSet.Contains(pm2Log.Fingerprint))
                continue;

            _db.PM2Logs.Add(pm2Log);
            existingSet.Add(pm2Log.Fingerprint);
            inserted++;
        }

        await _db.SaveChangesAsync();
        var duplicates = request.Logs.Count - inserted - droppedNoProcess;
        return Ok(new { received = request.Logs.Count, inserted, duplicates, droppedNoProcess });
    }

    private static string CreateLogFingerprint(Guid serverId, Guid processId, LogStreamType streamType, DateTimeOffset timestamp, string message)
    {
        var raw = $"{serverId}|{processId}|{streamType}|{timestamp.ToUniversalTime():O}|{message}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string CreateSystemdFingerprint(Guid serverId, Guid serviceId, string? cursor, DateTimeOffset timestamp, string message)
    {
        var raw = $"{serverId}|{serviceId}|{cursor}|{timestamp.ToUniversalTime():O}|{message}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string MapPriority(int? priority) => priority switch
    {
        0 or 1 or 2 or 3 => "error",
        4 => "warn",
        7 => "debug",
        _ => "info"
    };

    private async Task<bool> ValidateAgentTokenAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearerToken = authHeader["Bearer ".Length..].Trim();
            var serverIdHeader = Request.Headers["X-Server-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(serverIdHeader) && Guid.TryParse(serverIdHeader, out var serverId))
            {
                var server = await _db.Servers.FindAsync(serverId);
                if (server is not null && string.Equals(server.ServerToken, bearerToken, StringComparison.Ordinal))
                    return true;
            }
        }

        var headerToken = Request.Headers["X-Agent-Token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerToken))
        {
            var configuredToken = _config["AgentSettings:Token"];
            if (!string.IsNullOrEmpty(configuredToken))
                return string.Equals(headerToken, configuredToken, StringComparison.Ordinal);
        }

        return false;
    }
}
