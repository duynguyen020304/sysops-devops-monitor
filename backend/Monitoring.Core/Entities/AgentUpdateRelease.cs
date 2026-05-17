namespace Monitoring.Core.Entities;

public class AgentUpdateRelease
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Version { get; set; }
    public required string BuildId { get; set; }
    public string Channel { get; set; } = "stable";
    public string? GitSha { get; set; }
    public required string ManifestJson { get; set; }
    public required string ManifestSignature { get; set; }
    public required string PublicKeyId { get; set; }
    public required string ArtifactPath { get; set; }
    public string ArtifactSha256 { get; set; } = string.Empty;
    public long ArtifactSize { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
