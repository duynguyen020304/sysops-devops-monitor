namespace Monitoring.Infrastructure.Services;

public sealed class WorkflowLogCacheOptions
{
    public bool Enabled { get; set; }
    public string? ConnectionString { get; set; }
    public string InstanceName { get; set; } = "monitoring";
    public int WorkflowLogTtlHours { get; set; } = 24;
}
