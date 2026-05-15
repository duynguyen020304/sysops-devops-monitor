namespace Monitoring.Core.Entities;

public class WorkflowRun
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public required string WorkflowName { get; set; }
    public long GithubRunId { get; set; }
    public required string Branch { get; set; }
    public required string CommitSha { get; set; }
    public required string CommitMessage { get; set; }
    public required string Actor { get; set; }
    public required string EventType { get; set; }
    public required string Status { get; set; }
    public string? Conclusion { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public required string HtmlUrl { get; set; }
}
