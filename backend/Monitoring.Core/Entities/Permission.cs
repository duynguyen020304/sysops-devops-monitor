namespace Monitoring.Core.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
