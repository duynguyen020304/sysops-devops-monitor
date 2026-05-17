using Microsoft.Extensions.Options;
using Monitoring.Core;
using Monitoring.Core.Interfaces;

namespace Monitoring.Api.Services;

public sealed class AgentCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AgentCleanupOptions> _options;
    private readonly ILogger<AgentCleanupHostedService> _logger;

    public AgentCleanupHostedService(IServiceScopeFactory scopeFactory, IOptionsMonitor<AgentCleanupOptions> options, ILogger<AgentCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            var interval = TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes));
            if (!options.Enabled)
            {
                _logger.LogInformation("Agent cleanup skipped; disabled. Next check in {Interval}.", interval);
                await Task.Delay(interval, stoppingToken);
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IAgentCleanupService>();
                var result = await cleanup.CleanupStaleAsync(
                    "automated stale cleanup",
                    TimeSpan.FromMinutes(Math.Max(1, options.TokenMaxAgeMinutes)),
                    TimeSpan.FromMinutes(Math.Max(1, options.ServerStaleThresholdMinutes)),
                    stoppingToken);
                _logger.LogInformation("Agent cleanup archived {TokensArchived} install tokens and {ServersArchived} server connections.", result.TokensArchived, result.ServersArchived);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent cleanup failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
