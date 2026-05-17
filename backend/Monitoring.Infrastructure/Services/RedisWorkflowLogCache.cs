using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;
using StackExchange.Redis;

namespace Monitoring.Infrastructure.Services;

public sealed class RedisWorkflowLogCache : IWorkflowLogCache, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkflowLogCacheOptions _options;
    private readonly ILogger<RedisWorkflowLogCache> _logger;
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisWorkflowLogCache(IOptions<WorkflowLogCacheOptions> options, ILogger<RedisWorkflowLogCache> logger)
    {
        _options = options.Value;
        _logger = logger;
        _connection = new Lazy<ConnectionMultiplexer>(CreateConnection);
    }

    public async Task<IReadOnlyList<WorkflowLog>?> GetAsync(Guid repositoryId, long githubRunId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await GetDatabase().StringGetAsync(Key(repositoryId, githubRunId));
            if (value.IsNullOrEmpty)
                return null;

            var bytes = (byte[])value!;
            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            var logs = await JsonSerializer.DeserializeAsync<List<WorkflowLog>>(gzip, JsonOptions, cancellationToken);
            return logs?
                .OrderBy(l => l.LineNumber)
                .ThenBy(l => l.Id)
                .ToList();
        }
        catch (Exception ex) when (ex is RedisException or IOException or JsonException or ObjectDisposedException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Workflow log Redis cache get failed for repository {RepositoryId}, run {GithubRunId}", repositoryId, githubRunId);
            return null;
        }
    }

    public async Task SetAsync(Guid repositoryId, long githubRunId, IReadOnlyList<WorkflowLog> logs, CancellationToken cancellationToken = default)
    {
        if (logs.Count == 0)
            return;

        try
        {
            await using var output = new MemoryStream();
            await using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                var ordered = logs.OrderBy(l => l.LineNumber).ThenBy(l => l.Id).ToList();
                await JsonSerializer.SerializeAsync(gzip, ordered, JsonOptions, cancellationToken);
            }

            var ttl = TimeSpan.FromHours(Math.Max(1, _options.WorkflowLogTtlHours));
            await GetDatabase().StringSetAsync(Key(repositoryId, githubRunId), output.ToArray(), ttl);
        }
        catch (Exception ex) when (ex is RedisException or IOException or JsonException or ObjectDisposedException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Workflow log Redis cache set failed for repository {RepositoryId}, run {GithubRunId}", repositoryId, githubRunId);
        }
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }

    private IDatabase GetDatabase() => _connection.Value.GetDatabase();

    private ConnectionMultiplexer CreateConnection()
    {
        var config = ConfigurationOptions.Parse(_options.ConnectionString ?? throw new InvalidOperationException("Redis connection string missing."));
        config.AbortOnConnectFail = false;
        if (config.ConnectTimeout < 5000)
            config.ConnectTimeout = 5000;
        if (config.SyncTimeout < 5000)
            config.SyncTimeout = 5000;

        return ConnectionMultiplexer.Connect(config);
    }

    private string Key(Guid repositoryId, long githubRunId) =>
        $"{_options.InstanceName.TrimEnd(':')}:workflow-logs:{repositoryId:N}:{githubRunId}";
}
