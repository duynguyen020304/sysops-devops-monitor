using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Monitoring.Core.Entities;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Services;

public class AgentReleasePublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<AgentReleasePublisherService> _logger;

    public AgentReleasePublisherService(IServiceScopeFactory scopeFactory, IWebHostEnvironment env, IConfiguration config, ILogger<AgentReleasePublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _env = env;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await PublishCurrentAgentAsync(stoppingToken); }
        catch (Exception ex) { _logger.LogError(ex, "Agent release auto-publish failed"); }
    }

    private async Task PublishCurrentAgentAsync(CancellationToken ct)
    {
        var privateKey = _config["AgentUpdates:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            _logger.LogWarning("Agent update signing key missing; auto-publish skipped.");
            return;
        }

        var agentDistPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "agent", "dist"));
        var packageJsonPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "agent", "package.json"));
        var lockPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "agent", "pnpm-lock.yaml"));
        if (!Directory.Exists(agentDistPath) || !File.Exists(packageJsonPath))
        {
            _logger.LogWarning("Agent dist/package missing; auto-publish skipped.");
            return;
        }

        using var packageDoc = JsonDocument.Parse(await File.ReadAllTextAsync(packageJsonPath, ct));
        var version = packageDoc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0";
        var buildId = _config["AGENT_BUILD_ID"] ?? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var releaseDir = Path.Combine(_env.ContentRootPath, "agent-releases"); Directory.CreateDirectory(releaseDir);
        var artifactPath = Path.Combine(releaseDir, $"monitoring-agent-{version}-{buildId}.tar.gz");
        if (!File.Exists(artifactPath)) await AgentReleasePackager.CreateTarAsync(agentDistPath, packageJsonPath, lockPath, artifactPath);
        var artifactBytes = await File.ReadAllBytesAsync(artifactPath, ct);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(artifactBytes)).ToLowerInvariant();
        var publicKeyId = _config["AgentUpdates:PublicKeyId"] ?? "prod";
        var manifest = new { schemaVersion = 1, version, buildId, gitSha = _config["GITHUB_SHA"] ?? Environment.GetEnvironmentVariable("GITHUB_SHA"), createdAt = DateTimeOffset.UtcNow.ToString("O"), expiresAt = DateTimeOffset.UtcNow.AddDays(14).ToString("O"), channel = "stable", artifactSha256 = sha, artifactSize = artifactBytes.LongLength };
        var manifestJson = AgentUpdateManifestTools.CanonicalizeJson(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var signature = AgentUpdateManifestTools.SignManifest(manifestJson, privateKey);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
        var workspaceIds = await db.Workspaces.Select(w => w.Id).ToListAsync(ct);
        foreach (var workspaceId in workspaceIds)
        {
            var exists = await db.AgentUpdateReleases.AnyAsync(r => r.WorkspaceId == workspaceId && r.BuildId == buildId, ct);
            if (exists) continue;
            db.AgentUpdateReleases.Add(new AgentUpdateRelease
            {
                Id = Guid.NewGuid(), WorkspaceId = workspaceId, Version = version, BuildId = buildId,
                ManifestJson = manifestJson, ManifestSignature = signature, PublicKeyId = publicKeyId,
                Channel = "stable", GitSha = _config["GITHUB_SHA"] ?? Environment.GetEnvironmentVariable("GITHUB_SHA"),
                ArtifactPath = artifactPath, ArtifactSha256 = sha, ArtifactSize = artifactBytes.LongLength,
                IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
