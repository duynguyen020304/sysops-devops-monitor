using Monitoring.Core.Enums;

namespace Monitoring.Core.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public AlertSourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public DateTimeOffset TriggeredAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? RuleId { get; set; }
}
