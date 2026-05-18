using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Infrastructure.Data;
using Monitoring.Api.Services;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/agent/update")]
public class AgentUpdateController : ControllerBase
{
    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AgentUpdateController(MonitoringDbContext db, IConfiguration config, IWebHostEnvironment env)
    { _db = db; _config = config; _env = env; }

    [HttpPost("check")]
    public async Task<ActionResult<AgentUpdateCheckResponse>> Check([FromBody] AgentUpdateCheckRequest request)
    {
        var server = await ValidateAgentTokenAsync();
        if (server is null) return Unauthorized(new { message = "Invalid agent token." });

        server.AgentVersion = request.CurrentVersion;
        server.AgentBuildId = request.BuildId;
        if (request.Capabilities is not null) server.AgentCapabilitiesJson = request.Capabilities.Value.GetRawText();
        server.UpdatedAt = DateTime.UtcNow;

        var assignment = await _db.AgentUpdateAssignments
            .Include(a => a.Release)
            .Where(a => a.ServerId == server.Id && (a.Status == "Pending" || a.Status == "Offered" || a.Status == "Downloading" || a.Status == "Verified" || a.Status == "Restarting"))
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (assignment is null)
        {
            await _db.SaveChangesAsync();
            return Ok(new AgentUpdateCheckResponse(null, "none", null, 300));
        }

        assignment.Status = "Offered";
        assignment.FromVersion ??= request.CurrentVersion;
        assignment.FromBuildId ??= request.BuildId;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        server.AgentUpdateStatus = assignment.Status;
        await _db.SaveChangesAsync();

        var manifest = JsonSerializer.Deserialize<JsonElement>(assignment.Release.ManifestJson);
        var artifactUrl = $"{GetBaseUrl()}/api/agent/update/artifacts/{assignment.Release.Id}";
        var offer = new AgentUpdateOfferDto(assignment.Release.Id, assignment.Release.Version, assignment.Release.BuildId, manifest, assignment.Release.ManifestSignature, assignment.Release.PublicKeyId, artifactUrl);
        return Ok(new AgentUpdateCheckResponse(assignment.Id, "update", offer));
    }

    [HttpPost("events")]
    public async Task<IActionResult> Events([FromBody] AgentUpdateEventRequest request)
    {
        var server = await ValidateAgentTokenAsync();
        if (server is null || server.Id != request.ServerId) return Unauthorized(new { message = "Invalid agent token." });

        AgentUpdateAssignment? assignment = null;
        if (request.AssignmentId is not null)
            assignment = await _db.AgentUpdateAssignments.FirstOrDefaultAsync(a => a.Id == request.AssignmentId && a.ServerId == server.Id);
        if (assignment is null && request.EventType.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            assignment = await _db.AgentUpdateAssignments
                .Where(a => a.ServerId == server.Id && (a.Status == "Pending" || a.Status == "Offered" || a.Status == "Downloading" || a.Status == "Verified" || a.Status == "Restarting"))
                .OrderBy(a => a.CreatedAt)
                .FirstOrDefaultAsync();

        var now = request.Timestamp ?? DateTimeOffset.UtcNow;
        _db.AgentUpdateEvents.Add(new AgentUpdateEvent
        {
            Id = Guid.NewGuid(), ServerId = server.Id, AssignmentId = assignment?.Id, EventType = request.EventType,
            Message = request.Message, MetadataJson = request.Metadata?.GetRawText(), Timestamp = now
        });

        if (assignment is not null)
        {
            assignment.Status = NormalizeEventStatus(request.EventType);
            assignment.UpdatedAt = DateTimeOffset.UtcNow;
            if (assignment.Status is "Succeeded" or "Failed") assignment.CompletedAt = DateTimeOffset.UtcNow;
            if (assignment.Status == "Failed")
            {
                assignment.ErrorMessage = request.Message;
                assignment.LastFailureCode = request.EventType;
                assignment.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(30);
            }
            server.AgentUpdateStatus = assignment.Status;
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = "Event recorded." });
    }

    [HttpGet("artifacts/{releaseId:guid}")]
    public async Task<IActionResult> Artifact(Guid releaseId)
    {
        var server = await ValidateAgentTokenAsync();
        if (server is null) return Unauthorized(new { message = "Invalid agent token." });
        var release = await _db.AgentUpdateReleases.FirstOrDefaultAsync(r => r.Id == releaseId && r.WorkspaceId == server.WorkspaceId);
        if (release is null || !System.IO.File.Exists(release.ArtifactPath)) return NotFound(new { message = "Artifact not found." });
        return PhysicalFile(release.ArtifactPath, "application/gzip", $"monitoring-agent-{release.Version}-{release.BuildId}.tar.gz");
    }

    [HttpGet("releases"), Authorize, RequirePermission("deploy_agents")]
    public async Task<ActionResult<List<AgentUpdateReleaseDto>>> Releases()
    {
        var workspaceId = await GetWorkspaceIdAsync(); if (workspaceId is null) return Unauthorized();
        var releases = await _db.AgentUpdateReleases.Where(r => r.WorkspaceId == workspaceId).OrderByDescending(r => r.CreatedAt).Select(r => MapRelease(r)).ToListAsync();
        return Ok(releases);
    }

    [HttpPost("releases/current"), Authorize, RequirePermission("deploy_agents")]
    public async Task<ActionResult<AgentUpdateReleaseDto>> CreateCurrentRelease()
    {
        var workspaceId = await GetWorkspaceIdAsync(); if (workspaceId is null) return Unauthorized();
        var release = await BuildCurrentReleaseAsync(workspaceId.Value);
        _db.AgentUpdateReleases.Add(release);
        await _db.SaveChangesAsync();
        return Ok(MapRelease(release));
    }

    [HttpPost("servers/{serverId:guid}/assign"), Authorize, RequirePermission("deploy_agents")]
    public async Task<ActionResult<AgentUpdateAssignmentDto>> Assign(Guid serverId, [FromBody] CreateAgentUpdateAssignmentRequest request)
    {
        var workspaceId = await GetWorkspaceIdAsync(); if (workspaceId is null) return Unauthorized();
        var server = await _db.Servers.FirstOrDefaultAsync(s => s.Id == serverId && s.WorkspaceId == workspaceId);
        if (server is null) return NotFound(new { message = "Server not found." });
        var release = await _db.AgentUpdateReleases.FirstOrDefaultAsync(r => r.Id == request.ReleaseId && r.WorkspaceId == workspaceId);
        if (release is null) return NotFound(new { message = "Release not found." });
        var activeStatuses = new[] { "Pending", "Offered", "Downloading", "Verified", "Restarting" };
        if (await _db.AgentUpdateAssignments.AnyAsync(a => a.ServerId == serverId && activeStatuses.Contains(a.Status)))
            return Conflict(new { message = "Active assignment already exists for this server." });
        var now = DateTimeOffset.UtcNow;
        var lastForRelease = await _db.AgentUpdateAssignments
            .Where(a => a.ServerId == serverId && a.ReleaseId == release.Id)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
        var retryCount = AgentUpdateRetryPolicy.NextRetryCount(lastForRelease);
        if (!AgentUpdateRetryPolicy.CanRetry(retryCount))
            return Conflict(new { message = "Max retries reached for this server and release." });
        var assignment = new AgentUpdateAssignment { Id = Guid.NewGuid(), ServerId = serverId, ReleaseId = release.Id, FromVersion = server.AgentVersion, FromBuildId = server.AgentBuildId, Status = "Pending", RetryCount = retryCount, CreatedAt = now, UpdatedAt = now };
        _db.AgentUpdateAssignments.Add(assignment);
        server.AgentUpdateStatus = "Pending";
        await _db.SaveChangesAsync();
        assignment.Release = release;
        return Ok(MapAssignment(assignment));
    }

    [HttpGet("servers/{serverId:guid}/assignments"), Authorize, RequirePermission("deploy_agents")]
    public async Task<ActionResult<List<AgentUpdateAssignmentDto>>> Assignments(Guid serverId)
    {
        var workspaceId = await GetWorkspaceIdAsync(); if (workspaceId is null) return Unauthorized();
        if (!await _db.Servers.AnyAsync(s => s.Id == serverId && s.WorkspaceId == workspaceId)) return NotFound();
        var rows = await _db.AgentUpdateAssignments.Include(a => a.Release).Where(a => a.ServerId == serverId).OrderByDescending(a => a.CreatedAt).Take(20).ToListAsync();
        return Ok(rows.Select(MapAssignment).ToList());
    }

    private async Task<Server?> ValidateAgentTokenAsync()
    {
        var auth = Request.Headers.Authorization.ToString();
        var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..] : null;
        var serverIdHeader = Request.Headers["X-Server-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(serverIdHeader, out var serverId)) return null;
        return await _db.Servers.FirstOrDefaultAsync(s => s.Id == serverId && s.ServerToken == token);
    }

    private async Task<Guid?> GetWorkspaceIdAsync()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var userId)) return null;
        return await _db.Users.Where(u => u.Id == userId).Select(u => (Guid?)u.WorkspaceId).FirstOrDefaultAsync();
    }

    private async Task<AgentUpdateRelease> BuildCurrentReleaseAsync(Guid workspaceId)
    {
        var agentDistPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "agent", "dist"));
        var packageJsonPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "agent", "package.json"));
        var lockPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "agent", "pnpm-lock.yaml"));
        if (!Directory.Exists(agentDistPath)) throw new InvalidOperationException("Agent dist not found. Build/deploy agent first.");
        using var packageDoc = JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(packageJsonPath));
        var version = packageDoc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0";
        var buildId = _config["AGENT_BUILD_ID"] ?? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var releaseDir = Path.Combine(_env.ContentRootPath, "agent-releases"); Directory.CreateDirectory(releaseDir);
        var artifactPath = Path.Combine(releaseDir, $"monitoring-agent-{version}-{buildId}.tar.gz");
        if (!System.IO.File.Exists(artifactPath)) await AgentReleasePackager.CreateTarAsync(agentDistPath, packageJsonPath, lockPath, artifactPath);
        var artifactBytes = await System.IO.File.ReadAllBytesAsync(artifactPath);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(artifactBytes)).ToLowerInvariant();
        var publicKeyId = _config["AgentUpdates:PublicKeyId"] ?? "dev";
        var manifest = new { schemaVersion = 1, version, buildId, gitSha = _config["GITHUB_SHA"] ?? Environment.GetEnvironmentVariable("GITHUB_SHA"), createdAt = DateTimeOffset.UtcNow.ToString("O"), expiresAt = DateTimeOffset.UtcNow.AddDays(14).ToString("O"), channel = "stable", artifactSha256 = sha, artifactSize = artifactBytes.LongLength };
        var manifestJson = AgentUpdateManifestTools.CanonicalizeJson(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var signature = _config["AgentUpdates:PrivateKeyPem"] is { Length: > 0 } privateKey ? AgentUpdateManifestTools.SignManifest(manifestJson, privateKey) : "UNSIGNED_DEV";
        return new AgentUpdateRelease { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Version = version, BuildId = buildId, ManifestJson = manifestJson, ManifestSignature = signature, PublicKeyId = publicKeyId, Channel = "stable", GitSha = _config["GITHUB_SHA"] ?? Environment.GetEnvironmentVariable("GITHUB_SHA"), ArtifactPath = artifactPath, ArtifactSha256 = sha, ArtifactSize = artifactBytes.LongLength, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
    }

    private string GetBaseUrl()
    {
        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : Request.Scheme;
        return $"{scheme}://{Request.Host.Value}";
    }

    private static string NormalizeEventStatus(string eventType) => eventType.ToLowerInvariant() switch
    {
        "downloading" => "Downloading", "verified" => "Verified", "restarting" => "Restarting", "succeeded" => "Succeeded", "failed" => "Failed", _ => eventType
    };
    private static AgentUpdateReleaseDto MapRelease(AgentUpdateRelease r) => new(r.Id, r.Version, r.BuildId, r.Channel, r.GitSha, r.IsActive, r.ArtifactSha256, r.ArtifactSize, r.CreatedAt);
    private static AgentUpdateAssignmentDto MapAssignment(AgentUpdateAssignment a) => new(a.Id, a.ServerId, a.ReleaseId, a.Release.Version, a.Release.BuildId, a.Status, a.FromVersion, a.FromBuildId, a.ErrorMessage, a.CreatedAt, a.UpdatedAt, a.CompletedAt);
}
