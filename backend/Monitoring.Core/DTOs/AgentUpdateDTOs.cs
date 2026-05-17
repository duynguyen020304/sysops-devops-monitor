using System.Text.Json;

namespace Monitoring.Core.DTOs;

public record AgentUpdateCheckRequest(string CurrentVersion, string BuildId, JsonElement? Capabilities);
public record AgentUpdateCheckResponse(Guid? AssignmentId, string Action, AgentUpdateOfferDto? Release, int? RetryAfterSeconds = null);
public record AgentUpdateOfferDto(Guid ReleaseId, string Version, string BuildId, JsonElement Manifest, string ManifestSignature, string PublicKeyId, string ArtifactUrl, bool Rollback = false);
public record AgentUpdateEventRequest(Guid ServerId, Guid? AssignmentId, string EventType, string? Message, JsonElement? Metadata, DateTimeOffset? Timestamp);
public record AgentUpdateReleaseDto(Guid Id, string Version, string BuildId, string Channel, string? GitSha, bool IsActive, string ArtifactSha256, long ArtifactSize, DateTimeOffset CreatedAt);
public record AgentUpdateAssignmentDto(Guid Id, Guid ServerId, Guid ReleaseId, string Version, string BuildId, string Status, string? FromVersion, string? FromBuildId, string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt);
public record CreateAgentUpdateAssignmentRequest(Guid ReleaseId);
