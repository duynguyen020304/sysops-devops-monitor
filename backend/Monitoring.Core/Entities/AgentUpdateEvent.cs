namespace Monitoring.Core.Entities;

public class AgentUpdateEvent
{
    public Guid Id { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid ServerId { get; set; }
    public required string EventType { get; set; }
    public string? Message { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public AgentUpdateAssignment? Assignment { get; set; }
}
