namespace Monitoring.Core.DTOs;

public record RegisterServerRequest(string Hostname, string IpAddress, string OperatingSystem, string AgentVersion, string? SshUsername = null, int? SshPort = null, string? SshPrivateKeyPath = null);
public record ServerDto(Guid Id, string Hostname, string IpAddress, string OperatingSystem, string AgentVersion, string Status, DateTimeOffset? LastHeartbeatAt, DateTime CreatedAt, string? SshUsername, int SshPort, string? SshPrivateKeyPath);
public record ServerHealthDto(string Status, DateTimeOffset? LastHeartbeatAt, int PM2ProcessCount, int AlertCount);
public record AgentHeartbeatRequest(Guid ServerId);
public record AgentMetricsRequest(Guid ServerId, SystemMetricsDto Metrics);
public record SystemMetricsDto(double CpuUsagePercent, long MemoryTotalBytes, long MemoryUsedBytes, double MemoryUsagePercent, long SwapUsedBytes, long DiskTotalBytes, long DiskUsedBytes, double DiskUsagePercent, long DiskReadBytesPerSecond, long DiskWriteBytesPerSecond, long NetworkRxBytesPerSecond, long NetworkTxBytesPerSecond, double LoadAverage1m, double LoadAverage5m, double LoadAverage15m);
public record AgentPM2Request(Guid ServerId, List<PM2ProcessDto> Processes);
public record PM2ProcessDto(int Pm2Id, string Name, int Pid, string Status, long UptimeSeconds, int RestartCount, double CpuUsage, long MemoryUsage, string ExecutionMode, string NodeVersion);
public record AgentLogsRequest(Guid ServerId, List<AgentLogEntry> Logs);
public record AgentLogEntry(Guid? ProcessId, string? ProcessName, string StreamType, string Level, string Message, DateTimeOffset? Timestamp = null);
