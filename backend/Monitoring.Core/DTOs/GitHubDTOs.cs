namespace Monitoring.Core.DTOs;

// API request/response DTOs
public record ConnectRepositoryRequest(string GitHubToken, string Owner, string Name);

public record RepositoryDto(
    Guid Id,
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    string Visibility,
    DateTime CreatedAt
);

public record WorkflowRunDto(
    Guid Id,
    string WorkflowName,
    long GithubRunId,
    string Branch,
    string CommitSha,
    string CommitMessage,
    string Actor,
    string EventType,
    string Status,
    string? Conclusion,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int? DurationSeconds,
    string HtmlUrl
);

public record WorkflowLogDto(
    Guid Id,
    string JobName,
    string StepName,
    DateTimeOffset Timestamp,
    string Level,
    string Message
);

public record WorkflowRunDetailDto(
    WorkflowRunDto Run,
    List<WorkflowLogDto> Logs
);

public record RepositoryStatsDto(
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    int CancelledRuns,
    double AverageDuration,
    double FailureRate
);

// Pagination
public record PagedResult<T>(
    List<T> Items,
    int Total,
    int Page,
    int PageSize
);

// GitHub API response DTOs
public record GitHubRepoInfo(
    long Id,
    string Name,
    string Full_Name,
    string Default_Branch,
    string Visibility
);

public record GitHubWorkflowRunsResponse(
    int Total_Count,
    List<GitHubWorkflowRun> Workflow_Runs
);

public record GitHubWorkflowRun(
    long Id,
    string Name,
    string Head_Branch,
    string Head_Sha,
    string Status,
    string? Conclusion,
    DateTimeOffset Created_At,
    DateTimeOffset? Updated_At,
    string Html_Url,
    GitHubActor Actor,
    string Event
);

public record GitHubActor(string Login);
