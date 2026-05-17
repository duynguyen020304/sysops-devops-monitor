namespace Monitoring.Core.Entities;

public class SystemdService
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public required string LoadState { get; set; }
    public required string ActiveState { get; set; }
    public required string SubState { get; set; }
    public string? Description { get; set; }
    public string? FragmentPath { get; set; }
    public int? MainPid { get; set; }
    public long? MemoryCurrent { get; set; }
    public long? CpuUsageNSec { get; set; }
    public int? RestartCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Server? Server { get; set; }
    public List<SystemdLog> Logs { get; set; } = [];
}
