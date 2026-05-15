using Monitoring.Core.Enums;

namespace Monitoring.Core.Entities;

public class WorkflowLog
{
    public Guid Id { get; set; }
    public Guid WorkflowRunId { get; set; }
    public required string JobName { get; set; }
    public required string StepName { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public required string RawMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
