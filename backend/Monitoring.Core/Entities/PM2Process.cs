using Monitoring.Core.Enums;

namespace Monitoring.Core.Entities;

public class PM2Process
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public int Pm2Id { get; set; }
    public required string Name { get; set; }
    public int Pid { get; set; }
    public PM2ProcessStatus Status { get; set; }
    public long UptimeSeconds { get; set; }
    public int RestartCount { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public required string ExecutionMode { get; set; }
    public required string NodeVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
