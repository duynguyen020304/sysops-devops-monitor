namespace Monitoring.Core.Entities;

public class UserRoleEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? GrantedByUserId { get; set; }
    public DateTime GrantedAt { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
