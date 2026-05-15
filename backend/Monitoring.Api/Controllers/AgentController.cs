using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        if (!ValidateAgentToken())
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
        if (!ValidateAgentToken())
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
        if (!ValidateAgentToken())
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
        if (!ValidateAgentToken())
            return Unauthorized(new { message = "Invalid agent token." });

        // Verify server exists
        var server = await _serverService.GetServerAsync(request.ServerId);
        if (server is null)
            return NotFound(new { message = "Server not found." });

        var now = DateTime.UtcNow;

        foreach (var logEntry in request.Logs)
        {
            var streamType = logEntry.StreamType.ToLowerInvariant() switch
            {
                "stderr" => LogStreamType.StdErr,
                _ => LogStreamType.StdOut
            };

            var pm2Log = new PM2Log
            {
                Id = Guid.NewGuid(),
                ServerId = request.ServerId,
                ProcessId = logEntry.ProcessId ?? Guid.Empty,
                StreamType = streamType,
                Timestamp = DateTimeOffset.UtcNow,
                Level = logEntry.Level,
                Message = logEntry.Message,
                RawMessage = logEntry.Message,
                CreatedAt = now
            };

            _db.PM2Logs.Add(pm2Log);
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = $"Recorded {request.Logs.Count} log entries." });
    }

    private bool ValidateAgentToken()
    {
        var headerToken = Request.Headers["X-Agent-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(headerToken))
            return false;

        var configuredToken = _config["AgentSettings:Token"];
        if (string.IsNullOrEmpty(configuredToken))
            return false;

        return string.Equals(headerToken, configuredToken, StringComparison.Ordinal);
    }
}
