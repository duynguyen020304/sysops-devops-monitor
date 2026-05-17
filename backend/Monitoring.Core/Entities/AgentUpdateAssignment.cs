namespace Monitoring.Core.Entities;

public class AgentUpdateAssignment
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public Guid ReleaseId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? FromVersion { get; set; }
    public string? FromBuildId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastFailureCode { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Server Server { get; set; } = null!;
    public AgentUpdateRelease Release { get; set; } = null!;
}
