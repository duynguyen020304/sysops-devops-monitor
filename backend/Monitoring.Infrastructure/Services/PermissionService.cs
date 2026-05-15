using Microsoft.EntityFrameworkCore;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly MonitoringDbContext _db;

    // Scoped cache: load permissions once per request per user
    private Guid? _cachedUserId;
    private HashSet<string>? _cachedPermissions;

    public PermissionService(MonitoringDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission)
    {
        var permissions = await GetOrLoadPermissionsAsync(userId);
        return permissions.Contains(permission.ToUpperInvariant());
    }

    public async Task<bool> HasAnyPermissionAsync(Guid userId, params string[] permissions)
    {
        var userPerms = await GetOrLoadPermissionsAsync(userId);
        return permissions.Any(p => userPerms.Contains(p.ToUpperInvariant()));
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId)
    {
        var permissions = await GetOrLoadPermissionsAsync(userId);
        return permissions.ToList().AsReadOnly();
    }

    private async Task<HashSet<string>> GetOrLoadPermissionsAsync(Guid userId)
    {
        if (_cachedUserId == userId && _cachedPermissions != null)
            return _cachedPermissions;

        _cachedPermissions = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
            .Join(_db.Permissions, permId => permId, p => p.Id, (_, p) => p.NormalizedName)
            .Distinct()
            .ToHashSetAsync();

        _cachedUserId = userId;
        return _cachedPermissions;
    }
}
