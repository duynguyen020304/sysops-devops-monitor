namespace Monitoring.Core;

public sealed class AgentCleanupOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 30;
    public int TokenMaxAgeMinutes { get; set; } = 60;
    public int ServerStaleThresholdMinutes { get; set; } = 2;
}
