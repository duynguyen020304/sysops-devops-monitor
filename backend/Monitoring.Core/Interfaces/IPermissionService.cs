namespace Monitoring.Core.Interfaces;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permission);
    Task<bool> HasAnyPermissionAsync(Guid userId, params string[] permissions);
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId);
}

