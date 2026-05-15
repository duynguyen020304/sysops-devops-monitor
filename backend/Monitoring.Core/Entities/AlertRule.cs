using Monitoring.Core.Enums;

namespace Monitoring.Core.Entities;

public class AlertRule
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Name { get; set; }
    public AlertSourceType SourceType { get; set; }
    public required string ConditionType { get; set; }
    public double Threshold { get; set; }
    public int TimeWindowSeconds { get; set; }
    public AlertSeverity Severity { get; set; }
    public bool IsEnabled { get; set; }
    public int CooldownSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
