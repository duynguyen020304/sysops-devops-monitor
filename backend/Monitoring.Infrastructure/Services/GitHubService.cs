using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class GitHubService : IGitHubService
{
    private readonly MonitoringDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IWorkflowLogCache _workflowLogCache;
    private const int MaxLogPageLimit = 1000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubService(MonitoringDbContext db, IHttpClientFactory httpClientFactory, IWorkflowLogCache workflowLogCache)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient("GitHub");
        _workflowLogCache = workflowLogCache;
    }

    public async Task<Repository> ConnectRepositoryAsync(Guid workspaceId, string githubToken, string owner, string name)
    {
        // Validate token and fetch repo info from GitHub
        SetAuthHeader(githubToken);

        var response = await _httpClient.GetAsync($"/repos/{owner}/{name}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var repoInfo = JsonSerializer.Deserialize<GitHubRepoInfo>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub repo info.");

        // Check if already connected
        var existing = await _db.Repositories
            .FirstOrDefaultAsync(r => r.GithubRepositoryId == repoInfo.Id);
        if (existing is not null)
            throw new InvalidOperationException("This repository is already connected.");

        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Provider = "github",
            AccessToken = githubToken,
            Owner = owner,
            Name = name,
            FullName = repoInfo.Full_Name,
            DefaultBranch = repoInfo.Default_Branch,
            Visibility = repoInfo.Visibility ?? "private",
            GithubRepositoryId = repoInfo.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Repositories.Add(repository);
        await _db.SaveChangesAsync();

        return repository;
    }

    public async Task<List<Repository>> GetRepositoriesAsync(Guid workspaceId)
    {
        return await _db.Repositories
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Repository?> GetRepositoryAsync(Guid repositoryId)
    {
        return await _db.Repositories.FindAsync(repositoryId);
    }

    public async Task DisconnectRepositoryAsync(Guid repositoryId)
    {
        var repository = await _db.Repositories.FindAsync(repositoryId)
            ?? throw new InvalidOperationException("Repository not found.");

        // Remove associated workflow runs and logs
        var runs = await _db.WorkflowRuns
            .Where(r => r.RepositoryId == repositoryId)
            .ToListAsync();

        var runIds = runs.Select(r => r.Id).ToList();
        var logs = await _db.WorkflowLogs
            .Where(l => runIds.Contains(l.WorkflowRunId))
            .ToListAsync();

        _db.WorkflowLogs.RemoveRange(logs);
        _db.WorkflowRuns.RemoveRange(runs);
        _db.Repositories.Remove(repository);

        await _db.SaveChangesAsync();
    }

    public async Task<List<WorkflowRun>> SyncWorkflowRunsAsync(Guid repositoryId)
    {
        var repository = await _db.Repositories.FindAsync(repositoryId)
            ?? throw new InvalidOperationException("Repository not found.");

        if (string.IsNullOrEmpty(repository.AccessToken))
            throw new InvalidOperationException("Repository has no access token. Please re-connect the repository.");
        SetAuthHeader(repository.AccessToken);

        var response = await _httpClient.GetAsync(
            $"/repos/{repository.Owner}/{repository.Name}/actions/runs?per_page=50");

        CheckRateLimit(response);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var runsResponse = JsonSerializer.Deserialize<GitHubWorkflowRunsResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize workflow runs.");

        var existingRuns = await _db.WorkflowRuns
            .Where(r => r.RepositoryId == repositoryId)
            .ToDictionaryAsync(r => r.GithubRunId);

        var result = new List<WorkflowRun>();

        foreach (var ghRun in runsResponse.Workflow_Runs)
        {
            if (existingRuns.TryGetValue(ghRun.Id, out var existing))
            {
                // Update existing run
                existing.Status = ghRun.Status;
                existing.Conclusion = ghRun.Conclusion;
                existing.CompletedAt = ghRun.Updated_At;

                if (ghRun.Status == "completed" && ghRun.Updated_At.HasValue)
                {
                    var duration = (ghRun.Updated_At.Value - ghRun.Created_At).TotalSeconds;
                    existing.DurationSeconds = (int)duration;
                }

                result.Add(existing);
            }
            else
            {
                // Insert new run
                var workflowRun = new WorkflowRun
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = repositoryId,
                    WorkflowName = ghRun.Name,
                    GithubRunId = ghRun.Id,
                    Branch = ghRun.Head_Branch,
                    CommitSha = ghRun.Head_Sha,
                    CommitMessage = "", // not available in the list endpoint
                    Actor = ghRun.Actor.Login,
                    EventType = ghRun.Event,
                    Status = ghRun.Status,
                    Conclusion = ghRun.Conclusion,
                    StartedAt = ghRun.Created_At,
                    CompletedAt = ghRun.Updated_At,
                    DurationSeconds = ghRun.Status == "completed" && ghRun.Updated_At.HasValue
                        ? (int)(ghRun.Updated_At.Value - ghRun.Created_At).TotalSeconds
                        : null,
                    HtmlUrl = ghRun.Html_Url
                };

                _db.WorkflowRuns.Add(workflowRun);
                result.Add(workflowRun);
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<List<WorkflowLog>> GetWorkflowLogsAsync(Guid repositoryId, long githubRunId)
    {
        var repository = await _db.Repositories.FindAsync(repositoryId)
            ?? throw new InvalidOperationException("Repository not found.");

        var workflowRun = await _db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.RepositoryId == repositoryId && r.GithubRunId == githubRunId)
            ?? throw new InvalidOperationException("Workflow run not found.");

        var cachedLogs = await _workflowLogCache.GetAsync(repositoryId, githubRunId);
        if (cachedLogs is { Count: > 0 })
            return cachedLogs.ToList();

        // Check if logs already persisted
        var existingLogs = await _db.WorkflowLogs
            .Where(l => l.WorkflowRunId == workflowRun.Id)
            .OrderBy(l => l.LineNumber)
            .ThenBy(l => l.Id)
            .ToListAsync();

        if (existingLogs.Count > 0)
        {
            await _workflowLogCache.SetAsync(repositoryId, githubRunId, existingLogs);
            return existingLogs;
        }

        // Fetch from GitHub
        if (string.IsNullOrEmpty(repository.AccessToken))
            throw new InvalidOperationException("Repository has no access token. Please re-connect the repository.");
        SetAuthHeader(repository.AccessToken);

        // GitHub returns 302 redirect to a .zip archive containing .txt log files
        var response = await _httpClient.GetAsync(
            $"/repos/{repository.Owner}/{repository.Name}/actions/runs/{githubRunId}/logs");

        CheckRateLimit(response);
        response.EnsureSuccessStatusCode();

        var zipBytes = await response.Content.ReadAsByteArrayAsync();
        var logs = new List<WorkflowLog>();

        using (var zipStream = new MemoryStream(zipBytes))
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            var lineNumber = 0;
            foreach (var entry in archive.Entries
                .Where(e => e.FullName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                var jobName = Path.GetFileNameWithoutExtension(entry.Name);
                using var reader = new StreamReader(entry.Open());
                var content = await reader.ReadToEndAsync();
                logs.AddRange(ParseLogContent(content, workflowRun.Id, jobName, ref lineNumber));
            }
        }

        _db.WorkflowLogs.AddRange(logs);
        await _db.SaveChangesAsync();

        var orderedLogs = logs
            .OrderBy(l => l.LineNumber)
            .ThenBy(l => l.Id)
            .ToList();
        await _workflowLogCache.SetAsync(repositoryId, githubRunId, orderedLogs);

        return orderedLogs;
    }

    public async Task<WorkflowLogPageDto> GetWorkflowLogPageAsync(Guid repositoryId, long githubRunId, string? cursor, int limit)
    {
        limit = Math.Clamp(limit, 1, MaxLogPageLimit);
        await GetWorkflowLogsAsync(repositoryId, githubRunId);

        var workflowRun = await _db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.RepositoryId == repositoryId && r.GithubRunId == githubRunId)
            ?? throw new InvalidOperationException("Workflow run not found.");

        var lastLineNumber = DecodeCursor(cursor);
        var query = _db.WorkflowLogs
            .AsNoTracking()
            .Where(l => l.WorkflowRunId == workflowRun.Id);

        if (lastLineNumber.HasValue)
            query = query.Where(l => l.LineNumber > lastLineNumber.Value);

        var logs = await query
            .OrderBy(l => l.LineNumber)
            .ThenBy(l => l.Id)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = logs.Count > limit;
        var pageLogs = logs.Take(limit).ToList();
        var nextCursor = hasMore && pageLogs.Count > 0
            ? EncodeCursor(pageLogs[^1].LineNumber)
            : null;

        return new WorkflowLogPageDto(
            pageLogs.Select(ToWorkflowLogDto).ToList(),
            nextCursor,
            hasMore,
            limit);
    }

    public async Task<RepositoryStatsDto> GetRepositoryStatsAsync(Guid repositoryId)
    {
        var repository = await _db.Repositories.FindAsync(repositoryId)
            ?? throw new InvalidOperationException("Repository not found.");

        var runs = await _db.WorkflowRuns
            .Where(r => r.RepositoryId == repositoryId)
            .ToListAsync();

        var totalRuns = runs.Count;
        var successfulRuns = runs.Count(r => r.Conclusion == "success");
        var failedRuns = runs.Count(r => r.Conclusion == "failure");
        var cancelledRuns = runs.Count(r => r.Conclusion == "cancelled");

        var completedRuns = runs.Where(r => r.DurationSeconds.HasValue).ToList();
        var averageDuration = completedRuns.Count > 0
            ? completedRuns.Average(r => r.DurationSeconds!.Value)
            : 0;

        var failureRate = totalRuns > 0
            ? (double)failedRuns / totalRuns * 100
            : 0;

        return new RepositoryStatsDto(
            TotalRuns: totalRuns,
            SuccessfulRuns: successfulRuns,
            FailedRuns: failedRuns,
            CancelledRuns: cancelledRuns,
            AverageDuration: Math.Round(averageDuration, 2),
            FailureRate: Math.Round(failureRate, 2)
        );
    }

    private void SetAuthHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static void CheckRateLimit(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var values))
        {
            var remaining = values.FirstOrDefault();
            if (int.TryParse(remaining, out var count) && count <= 10)
            {
                // Log warning about approaching rate limit
                Console.WriteLine($"GitHub API rate limit approaching: {remaining} requests remaining.");
            }
        }
    }

    private static List<WorkflowLog> ParseLogContent(string logContent, Guid workflowRunId, string jobName, ref int lineNumber)
    {
        var logs = new List<WorkflowLog>();
        var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var currentJob = jobName;
        var currentStep = "unknown";

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd((char)13);
            lineNumber++;

            // Parse job/step headers from log format
            if (line.StartsWith("##[group]"))
            {
                var group = line["##[group]".Length..].Trim();
                if (group.Contains('('))
                {
                    currentJob = group[..group.IndexOf('(')].Trim();
                }
                continue;
            }

            if (line.StartsWith("##[section]"))
            {
                currentStep = line["##[section]".Length..].Trim();
                continue;
            }

            // Determine log level
            var level = "info";
            var lower = line.ToLowerInvariant();
            if (lower.Contains("error") || lower.Contains("fatal"))
                level = "error";
            else if (lower.Contains("warning") || lower.Contains("warn"))
                level = "warning";

            logs.Add(new WorkflowLog
            {
                Id = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                JobName = currentJob,
                StepName = currentStep,
                LineNumber = lineNumber,
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                Message = line,
                RawMessage = line,
                CreatedAt = DateTime.UtcNow
            });
        }

        return logs;
    }

    private static WorkflowLogDto ToWorkflowLogDto(WorkflowLog log) => new(
        Id: log.Id,
        LineNumber: log.LineNumber,
        JobName: log.JobName,
        StepName: log.StepName,
        Timestamp: log.Timestamp,
        Level: log.Level,
        Message: log.Message,
        RawMessage: log.RawMessage
    );

    private static string EncodeCursor(int lineNumber) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(lineNumber.ToString()));

    private static int? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(decoded, out var lineNumber) ? lineNumber : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
