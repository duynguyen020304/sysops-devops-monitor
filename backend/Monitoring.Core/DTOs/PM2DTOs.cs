namespace Monitoring.Core.DTOs;

public record PM2ProcessDetailDto(Guid Id, int Pm2Id, string Name, int Pid, string Status, long UptimeSeconds, int RestartCount, double CpuUsage, long MemoryUsage, string ExecutionMode, string NodeVersion, DateTime CreatedAt);
public record PM2LogDto(Guid Id, string StreamType, DateTimeOffset Timestamp, string Level, string Message);
public record PM2RestartDto(DateTimeOffset Timestamp, string OldStatus, string NewStatus);
