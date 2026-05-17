using Microsoft.EntityFrameworkCore;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class PM2Service : IPM2Service
{
    private readonly MonitoringDbContext _db;

    public PM2Service(MonitoringDbContext db)
    {
        _db = db;
    }

    public async Task ProcessPM2DataAsync(Guid serverId, List<PM2ProcessDto> processes)
    {
        var existingProcessRows = await _db.PM2Processes
            .Where(p => p.ServerId == serverId)
            .ToListAsync();

        var existingProcesses = existingProcessRows
            .GroupBy(p => p.Name)
            .Select(g => g.OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.CreatedAt).First())
            .ToDictionary(p => p.Name);

        var now = DateTime.UtcNow;

        foreach (var dto in processes)
        {
            if (existingProcesses.TryGetValue(dto.Name, out var existing))
            {
                // Detect status change (potential restart)
                var oldStatus = existing.Status;
                var newStatus = ParsePM2Status(dto.Status);

                if (oldStatus != newStatus)
                {
                    // Log the status change as a PM2 log entry
                    var message = $"Status changed from {oldStatus} to {newStatus}";
                    _db.PM2Logs.Add(new PM2Log
                    {
                        Id = Guid.NewGuid(),
                        ServerId = serverId,
                        ProcessId = existing.Id,
                        StreamType = LogStreamType.StdOut,
                        Timestamp = DateTimeOffset.UtcNow,
                        Level = newStatus == PM2ProcessStatus.Errored ? "error" : "info",
                        Message = message,
                        RawMessage = message,
                        Fingerprint = $"status:{serverId}:{existing.Id}:{oldStatus}:{newStatus}:{now:O}",
                        CreatedAt = now
                    });
                }

                // Update existing process
                existing.Pm2Id = dto.Pm2Id;
                existing.Pid = dto.Pid;
                existing.Status = newStatus;
                existing.UptimeSeconds = dto.UptimeSeconds;
                existing.RestartCount = dto.RestartCount;
                existing.CpuUsage = dto.CpuUsage;
                existing.MemoryUsage = dto.MemoryUsage;
                existing.ExecutionMode = dto.ExecutionMode;
                existing.NodeVersion = dto.NodeVersion;
                existing.UpdatedAt = now;
            }
            else
            {
                // Insert new process
                var process = new PM2Process
                {
                    Id = Guid.NewGuid(),
                    ServerId = serverId,
                    Pm2Id = dto.Pm2Id,
                    Name = dto.Name,
                    Pid = dto.Pid,
                    Status = ParsePM2Status(dto.Status),
                    UptimeSeconds = dto.UptimeSeconds,
                    RestartCount = dto.RestartCount,
                    CpuUsage = dto.CpuUsage,
                    MemoryUsage = dto.MemoryUsage,
                    ExecutionMode = dto.ExecutionMode,
                    NodeVersion = dto.NodeVersion,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.PM2Processes.Add(process);
            }
        }

        // Mark processes that are no longer reported as Stopped
        var reportedNames = processes.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var staleProcesses = existingProcesses.Values
            .Where(p => !reportedNames.Contains(p.Name) && p.Status != PM2ProcessStatus.Stopped)
            .ToList();

        foreach (var stale in staleProcesses)
        {
            stale.Status = PM2ProcessStatus.Stopped;
            stale.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<PM2Process>> GetProcessesByServerAsync(Guid serverId)
    {
        var processes = await _db.PM2Processes
            .Where(p => p.ServerId == serverId)
            .ToListAsync();

        return processes
            .GroupBy(p => p.Name)
            .Select(g => g.OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.CreatedAt).First())
            .OrderBy(p => p.Pm2Id)
            .ToList();
    }

    public async Task<PM2Process?> GetProcessAsync(Guid processId)
    {
        return await _db.PM2Processes.FindAsync(processId);
    }

    public async Task<List<PM2Log>> GetProcessLogsAsync(Guid processId, int limit = 100)
    {
        var process = await _db.PM2Processes.FindAsync(processId);
        if (process is null) return [];

        var siblingIds = await _db.PM2Processes
            .Where(p => p.ServerId == process.ServerId && p.Name == process.Name)
            .Select(p => p.Id)
            .ToListAsync();

        return await _db.PM2Logs
            .Where(l => siblingIds.Contains(l.ProcessId))
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .Take(limit)
            .ToListAsync();
    }

    private static PM2ProcessStatus ParsePM2Status(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "online" => PM2ProcessStatus.Online,
            "stopped" => PM2ProcessStatus.Stopped,
            "errored" or "error" => PM2ProcessStatus.Errored,
            "launching" => PM2ProcessStatus.Launching,
            _ => PM2ProcessStatus.Unknown
        };
    }
}
