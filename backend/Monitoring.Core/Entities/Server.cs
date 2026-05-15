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
    public ServerStatus Status { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
