using Microsoft.EntityFrameworkCore;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class ServerService : IServerService
{
    private readonly MonitoringDbContext _db;

    public ServerService(MonitoringDbContext db)
    {
        _db = db;
    }

    public async Task<Server> RegisterServerAsync(Guid workspaceId, RegisterServerRequest request)
    {
        var server = new Server
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Hostname = request.Hostname,
            IpAddress = request.IpAddress,
            OperatingSystem = request.OperatingSystem,
            AgentVersion = request.AgentVersion,
            Status = ServerStatus.Unknown,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Servers.Add(server);
        await _db.SaveChangesAsync();

        return server;
    }

    public async Task<List<Server>> GetServersAsync(Guid workspaceId)
    {
        return await _db.Servers
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<Server?> GetServerAsync(Guid serverId)
    {
        return await _db.Servers.FindAsync(serverId);
    }

    public async Task UpdateServerAsync(Guid serverId, RegisterServerRequest request)
    {
        var server = await _db.Servers.FindAsync(serverId)
            ?? throw new InvalidOperationException("Server not found.");

        server.Hostname = request.Hostname;
        server.IpAddress = request.IpAddress;
        server.OperatingSystem = request.OperatingSystem;
        server.AgentVersion = request.AgentVersion;
        server.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteServerAsync(Guid serverId)
    {
        var server = await _db.Servers.FindAsync(serverId)
            ?? throw new InvalidOperationException("Server not found.");

        // Remove related metrics
        var metrics = await _db.ServerMetrics
            .Where(m => m.ServerId == serverId)
            .ToListAsync();

        // Remove related PM2 logs
        var pm2Logs = await _db.PM2Logs
            .Where(l => l.ServerId == serverId)
            .ToListAsync();

        // Remove related PM2 processes
        var pm2Processes = await _db.PM2Processes
            .Where(p => p.ServerId == serverId)
            .ToListAsync();

        // Remove related alerts
        var alerts = await _db.Alerts
            .Where(a => a.SourceType == AlertSourceType.Server && a.SourceId == serverId)
            .ToListAsync();

        _db.Alerts.RemoveRange(alerts);
        _db.PM2Logs.RemoveRange(pm2Logs);
        _db.PM2Processes.RemoveRange(pm2Processes);
        _db.ServerMetrics.RemoveRange(metrics);
        _db.Servers.Remove(server);

        await _db.SaveChangesAsync();
    }

    public async Task ProcessHeartbeatAsync(Guid serverId)
    {
        var server = await _db.Servers.FindAsync(serverId)
            ?? throw new InvalidOperationException("Server not found.");

        server.LastHeartbeatAt = DateTimeOffset.UtcNow;
        server.Status = ServerStatus.Healthy;
        server.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task ProcessMetricsAsync(Guid serverId, SystemMetricsDto metrics)
    {
        var server = await _db.Servers.FindAsync(serverId)
            ?? throw new InvalidOperationException("Server not found.");

        var serverMetric = new ServerMetric
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            Timestamp = DateTimeOffset.UtcNow,
            CpuUsagePercent = metrics.CpuUsagePercent,
            MemoryTotalBytes = metrics.MemoryTotalBytes,
            MemoryUsedBytes = metrics.MemoryUsedBytes,
            MemoryUsagePercent = metrics.MemoryUsagePercent,
            SwapUsedBytes = metrics.SwapUsedBytes,
            DiskTotalBytes = metrics.DiskTotalBytes,
            DiskUsedBytes = metrics.DiskUsedBytes,
            DiskUsagePercent = metrics.DiskUsagePercent,
            DiskReadBytesPerSecond = metrics.DiskReadBytesPerSecond,
            DiskWriteBytesPerSecond = metrics.DiskWriteBytesPerSecond,
            NetworkRxBytesPerSecond = metrics.NetworkRxBytesPerSecond,
            NetworkTxBytesPerSecond = metrics.NetworkTxBytesPerSecond,
            LoadAverage1m = metrics.LoadAverage1m,
            LoadAverage5m = metrics.LoadAverage5m,
            LoadAverage15m = metrics.LoadAverage15m
        };

        _db.ServerMetrics.Add(serverMetric);

        // Check thresholds and create alerts
        await CheckThresholdsAsync(server, metrics);

        await _db.SaveChangesAsync();
    }

    public async Task<ServerHealthDto> GetServerHealthAsync(Guid serverId)
    {
        var server = await _db.Servers.FindAsync(serverId)
            ?? throw new InvalidOperationException("Server not found.");

        var pm2ProcessCount = await _db.PM2Processes
            .CountAsync(p => p.ServerId == serverId);

        var alertCount = await _db.Alerts
            .CountAsync(a => a.SourceType == AlertSourceType.Server
                             && a.SourceId == serverId
                             && a.Status != AlertStatus.Resolved);

        var status = CalculateStatus(server);

        return new ServerHealthDto(
            Status: status.ToString(),
            LastHeartbeatAt: server.LastHeartbeatAt,
            PM2ProcessCount: pm2ProcessCount,
            AlertCount: alertCount
        );
    }

    private static ServerStatus CalculateStatus(Server server)
    {
        // If no heartbeat received yet, status is Unknown
        if (server.LastHeartbeatAt is null)
            return ServerStatus.Unknown;

        // If last heartbeat is more than 2 minutes ago, status is Unknown
        var heartbeatAge = DateTimeOffset.UtcNow - server.LastHeartbeatAt.Value;
        if (heartbeatAge > TimeSpan.FromMinutes(2))
            return ServerStatus.Unknown;

        return server.Status;
    }

    private async Task CheckThresholdsAsync(Server server, SystemMetricsDto metrics)
    {
        // CPU threshold: alert if > 90%
        if (metrics.CpuUsagePercent > 90)
        {
            var existingAlert = await _db.Alerts.FirstOrDefaultAsync(a =>
                a.SourceType == AlertSourceType.Server
                && a.SourceId == server.Id
                && a.Title.Contains("CPU")
                && a.Status != AlertStatus.Resolved);

            if (existingAlert is null)
            {
                _db.Alerts.Add(new Alert
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = server.WorkspaceId,
                    SourceType = AlertSourceType.Server,
                    SourceId = server.Id,
                    Title = $"High CPU usage on {server.Hostname}",
                    Description = $"CPU usage is at {metrics.CpuUsagePercent:F1}% (threshold: 90%)",
                    Severity = AlertSeverity.Warning,
                    Status = AlertStatus.Triggered,
                    TriggeredAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Memory threshold: alert if > 90%
        if (metrics.MemoryUsagePercent > 90)
        {
            var existingAlert = await _db.Alerts.FirstOrDefaultAsync(a =>
                a.SourceType == AlertSourceType.Server
                && a.SourceId == server.Id
                && a.Title.Contains("Memory")
                && a.Status != AlertStatus.Resolved);

            if (existingAlert is null)
            {
                _db.Alerts.Add(new Alert
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = server.WorkspaceId,
                    SourceType = AlertSourceType.Server,
                    SourceId = server.Id,
                    Title = $"High memory usage on {server.Hostname}",
                    Description = $"Memory usage is at {metrics.MemoryUsagePercent:F1}% (threshold: 90%)",
                    Severity = AlertSeverity.Warning,
                    Status = AlertStatus.Triggered,
                    TriggeredAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Disk threshold: alert if > 90%
        if (metrics.DiskUsagePercent > 90)
        {
            var existingAlert = await _db.Alerts.FirstOrDefaultAsync(a =>
                a.SourceType == AlertSourceType.Server
                && a.SourceId == server.Id
                && a.Title.Contains("Disk")
                && a.Status != AlertStatus.Resolved);

            if (existingAlert is null)
            {
                _db.Alerts.Add(new Alert
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = server.WorkspaceId,
                    SourceType = AlertSourceType.Server,
                    SourceId = server.Id,
                    Title = $"High disk usage on {server.Hostname}",
                    Description = $"Disk usage is at {metrics.DiskUsagePercent:F1}% (threshold: 90%)",
                    Severity = AlertSeverity.Warning,
                    Status = AlertStatus.Triggered,
                    TriggeredAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Load average threshold: alert if > 10 (1-minute average)
        if (metrics.LoadAverage1m > 10)
        {
            var existingAlert = await _db.Alerts.FirstOrDefaultAsync(a =>
                a.SourceType == AlertSourceType.Server
                && a.SourceId == server.Id
                && a.Title.Contains("Load")
                && a.Status != AlertStatus.Resolved);

            if (existingAlert is null)
            {
                _db.Alerts.Add(new Alert
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = server.WorkspaceId,
                    SourceType = AlertSourceType.Server,
                    SourceId = server.Id,
                    Title = $"High load average on {server.Hostname}",
                    Description = $"1-minute load average is {metrics.LoadAverage1m:F2} (threshold: 10)",
                    Severity = AlertSeverity.Critical,
                    Status = AlertStatus.Triggered,
                    TriggeredAt = DateTimeOffset.UtcNow
                });
            }
        }
    }
}
