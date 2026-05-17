namespace Monitoring.Core.Entities;

public class SystemdLog
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public Guid ServiceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public int? Priority { get; set; }
    public required string Level { get; set; }
    public required string Message { get; set; }
    public required string RawJson { get; set; }
    public string? Cursor { get; set; }
    public string? BootId { get; set; }
    public required string Fingerprint { get; set; }
    public DateTime CreatedAt { get; set; }

    public SystemdService? Service { get; set; }
}
