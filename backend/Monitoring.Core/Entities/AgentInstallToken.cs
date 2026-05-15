namespace Monitoring.Core.Entities;

public class AgentInstallToken
{
    public Guid Id { get; set; }
    public required string Token { get; set; }
    public required string PasswordHash { get; set; }
    public Guid? ServerId { get; set; }
    public required string ServerName { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid WorkspaceId { get; set; }
}
