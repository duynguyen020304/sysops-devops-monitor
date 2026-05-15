using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;

namespace Monitoring.Infrastructure.Data;

public static class RbacSeeder
{
    public static async Task SeedAsync(MonitoringDbContext db, IConfiguration config)
    {
        await SeedPermissionsAsync(db);
        await SeedRolesAsync(db);
        await SeedSuperAdminAsync(db, config);
        await db.SaveChangesAsync();
    }

    private static async Task SeedPermissionsAsync(MonitoringDbContext db)
    {
        var systemPermissions = new[]
        {
            new { Name = "manage_workspace", Category = "Workspace", Description = "Manage workspace settings" },
            new { Name = "manage_users", Category = "Users", Description = "Create/edit/delete users" },
            new { Name = "view_users", Category = "Users", Description = "View user list" },
            new { Name = "connect_repos", Category = "Repositories", Description = "Connect/manage repositories" },
            new { Name = "connect_agents", Category = "Servers", Description = "Register/manage agents" },
            new { Name = "view_servers", Category = "Servers", Description = "View server list" },
            new { Name = "deploy_agents", Category = "Servers", Description = "Deploy agents to servers" },
            new { Name = "view_dashboards", Category = "Dashboards", Description = "View dashboards" },
            new { Name = "view_audit_logs", Category = "Logs", Description = "View action/audit logs" },
            new { Name = "view_pm2_logs", Category = "Logs", Description = "View PM2 process logs" },
            new { Name = "configure_alerts", Category = "Alerts", Description = "Create/edit alert rules" },
            new { Name = "acknowledge_alerts", Category = "Alerts", Description = "Acknowledge alerts" },
            new { Name = "resolve_alerts", Category = "Alerts", Description = "Resolve alerts" },
            new { Name = "manage_alerts", Category = "Alerts", Description = "Full alert management" },
            new { Name = "view_metrics", Category = "Metrics", Description = "View server metrics" },
            new { Name = "manage_retention", Category = "Retention", Description = "Manage data retention policies" },
        };

        var existing = await db.Permissions.Select(p => p.NormalizedName).ToHashSetAsync();

        foreach (var perm in systemPermissions)
        {
            var normalized = perm.Name.ToUpperInvariant();
            if (!existing.Contains(normalized))
            {
                db.Permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = perm.Name,
                    NormalizedName = normalized,
                    Category = perm.Category,
                    Description = perm.Description,
                    IsSystem = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                existing.Add(normalized);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(MonitoringDbContext db)
    {
        var allPermissions = await db.Permissions.ToDictionaryAsync(p => p.NormalizedName, p => p.Id);
        var existingRoles = await db.Roles.Select(r => r.NormalizedName).ToHashSetAsync();

        var roleDefinitions = new[]
        {
            new { Name = "Super Admin", NormalizedName = "SUPER ADMIN", Description = "Full system access", Permissions = allPermissions.Keys.ToList() },
            new { Name = "Owner", NormalizedName = "OWNER", Description = "Workspace owner with full access", Permissions = allPermissions.Keys.ToList() },
            new { Name = "Admin", NormalizedName = "ADMIN", Description = "Administrator, cannot manage workspace", Permissions = allPermissions.Keys.Where(k => k != "MANAGE_WORKSPACE").ToList() },
            new { Name = "DevOps Engineer", NormalizedName = "DEVOPS ENGINEER", Description = "DevOps engineer", Permissions = new[] { "CONNECT_REPOS", "CONNECT_AGENTS", "VIEW_SERVERS", "DEPLOY_AGENTS", "VIEW_DASHBOARDS", "VIEW_PM2_LOGS", "CONFIGURE_ALERTS", "ACKNOWLEDGE_ALERTS", "RESOLVE_ALERTS", "MANAGE_ALERTS", "VIEW_METRICS", "VIEW_USERS", "VIEW_AUDIT_LOGS" }.ToList() },
            new { Name = "Developer", NormalizedName = "DEVELOPER", Description = "Developer with read access", Permissions = new[] { "VIEW_DASHBOARDS", "VIEW_METRICS", "VIEW_PM2_LOGS", "VIEW_SERVERS", "VIEW_USERS" }.ToList() },
            new { Name = "Viewer", NormalizedName = "VIEWER", Description = "Read-only access", Permissions = new[] { "VIEW_DASHBOARDS", "VIEW_METRICS", "VIEW_SERVERS" }.ToList() },
        };

        foreach (var roleDef in roleDefinitions)
        {
            if (!existingRoles.Contains(roleDef.NormalizedName))
            {
                var roleId = Guid.NewGuid();
                db.Roles.Add(new Role
                {
                    Id = roleId,
                    Name = roleDef.Name,
                    NormalizedName = roleDef.NormalizedName,
                    Description = roleDef.Description,
                    IsSystem = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                foreach (var permName in roleDef.Permissions)
                {
                    if (allPermissions.TryGetValue(permName, out var permId))
                    {
                        db.RolePermissions.Add(new RolePermission
                        {
                            Id = Guid.NewGuid(),
                            RoleId = roleId,
                            PermissionId = permId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSuperAdminAsync(MonitoringDbContext db, IConfiguration config)
    {
        var email = config["SUPER_ADMIN_EMAIL"] ?? "admin@monitoring.local";
        var password = config["SUPER_ADMIN_PASSWORD"];

        if (string.IsNullOrEmpty(password))
        {
            Console.WriteLine("[RBAC Seed] SUPER_ADMIN_PASSWORD not set. Skipping super admin user creation.");
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email))
            return;

        // Get or create a workspace for the super admin
        var workspace = await db.Workspaces.FirstOrDefaultAsync();
        if (workspace is null)
        {
            workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = "System Workspace",
                OwnerUserId = Guid.NewGuid(), // will be updated
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Workspaces.Add(workspace);
            await db.SaveChangesAsync();
        }

        var superAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "SUPER ADMIN");
        if (superAdminRole is null)
            return;

        var user = new User
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "Super Admin",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        workspace.OwnerUserId = user.Id;

        db.Users.Add(user);
        db.UserRoles.Add(new UserRoleEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = superAdminRole.Id,
            GrantedAt = DateTime.UtcNow
        });

        Console.WriteLine($"[RBAC Seed] Created super admin user: {email}");
    }
}
