using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
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
            return Ok(new { message = "Heartbeat received." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
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
        var processIdsByName = await _db.PM2Processes
            .Where(p => p.ServerId == request.ServerId)
            .GroupBy(p => p.Name)
            .Select(g => new { Name = g.Key, Id = g.OrderByDescending(p => p.UpdatedAt).First().Id })
            .ToDictionaryAsync(p => p.Name, p => p.Id);

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
