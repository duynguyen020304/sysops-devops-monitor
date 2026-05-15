namespace Monitoring.Core.Entities;

public class ServerMetric
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryTotalBytes { get; set; }
    public long MemoryUsedBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    public long SwapUsedBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public long DiskUsedBytes { get; set; }
    public double DiskUsagePercent { get; set; }
    public long DiskReadBytesPerSecond { get; set; }
    public long DiskWriteBytesPerSecond { get; set; }
    public long NetworkRxBytesPerSecond { get; set; }
    public long NetworkTxBytesPerSecond { get; set; }
    public double LoadAverage1m { get; set; }
    public double LoadAverage5m { get; set; }
    public double LoadAverage15m { get; set; }
}
