namespace Monitoring.Core.Entities;

public class RolePermission
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
