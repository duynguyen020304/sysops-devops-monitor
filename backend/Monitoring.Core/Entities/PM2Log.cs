using Monitoring.Core.Enums;

namespace Monitoring.Core.Entities;

public class PM2Log
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public Guid ProcessId { get; set; }
    public LogStreamType StreamType { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public required string RawMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
