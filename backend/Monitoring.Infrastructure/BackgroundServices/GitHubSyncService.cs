using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.BackgroundServices;

public class GitHubSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GitHubSyncService> _logger;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

    public GitHubSyncService(IServiceProvider serviceProvider, ILogger<GitHubSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GitHubSyncService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllRepositoriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during GitHub sync cycle.");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }

        _logger.LogInformation("GitHubSyncService stopped.");
    }

    private async Task SyncAllRepositoriesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

        var repositories = await db.Repositories.ToListAsync(cancellationToken);

        if (repositories.Count == 0)
            return;

        _logger.LogInformation("Syncing {Count} repositories...", repositories.Count);

        foreach (var repository in repositories)
        {
            try
            {
                var gitHubService = scope.ServiceProvider
                    .GetRequiredService<Core.Interfaces.IGitHubService>();

                var runs = await gitHubService.SyncWorkflowRunsAsync(repository.Id);
                _logger.LogInformation(
                    "Synced {RunCount} runs for repository {RepoName}.",
                    runs.Count, repository.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to sync repository {RepoName} (Id: {RepoId}).",
                    repository.FullName, repository.Id);
            }
        }
    }
}
