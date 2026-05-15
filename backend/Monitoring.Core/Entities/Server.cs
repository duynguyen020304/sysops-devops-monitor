using Monitoring.Core.Enums;

namespace Monitoring.Core.Entities;

public class Server
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Hostname { get; set; }
    public required string IpAddress { get; set; }
    public required string OperatingSystem { get; set; }
    public required string AgentVersion { get; set; }
    public string? ServerToken { get; set; }
    public string? SshUsername { get; set; } = "root";
    public int SshPort { get; set; } = 22;
    public string? SshPrivateKeyPath { get; set; }
    public ServerStatus Status { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
