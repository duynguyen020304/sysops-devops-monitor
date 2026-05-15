namespace Monitoring.Core.Entities;

public class Workspace
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
