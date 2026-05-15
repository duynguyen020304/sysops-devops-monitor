using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;

namespace Monitoring.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    public bool HasPermission(UserRole role, string permission)
    {
        return permission.ToLowerInvariant() switch
        {
            "manage_workspace" => CanManageWorkspace(role),
            "manage_users" => CanManageUsers(role),
            "connect_repos" => CanConnectRepos(role),
            "connect_agents" => CanConnectAgents(role),
            "view_dashboards" => true,
            "view_actions_logs" => true,
            "view_pm2_logs" => role is not UserRole.Viewer,
            "configure_alerts" => CanConfigureAlerts(role),
            "acknowledge_alerts" => role is not UserRole.Viewer,
            "resolve_alerts" => CanResolveAlerts(role),
            "manage_retention" => CanManageRetention(role),
            "view_metrics" => CanViewMetrics(role),
            "manage_alerts" => CanManageAlerts(role),
            _ => false
        };
    }

    public bool CanManageUsers(UserRole role)
        => role is UserRole.Owner or UserRole.Admin;

    public bool CanManageWorkspace(UserRole role)
        => role is UserRole.Owner;

    public bool CanConnectRepos(UserRole role)
        => role is UserRole.Owner or UserRole.Admin or UserRole.DevOpsEngineer;

    public bool CanConnectAgents(UserRole role)
        => role is UserRole.Owner or UserRole.Admin or UserRole.DevOpsEngineer;

    public bool CanConfigureAlerts(UserRole role)
        => role is UserRole.Owner or UserRole.Admin or UserRole.DevOpsEngineer;

    public bool CanResolveAlerts(UserRole role)
        => role is UserRole.Owner or UserRole.Admin or UserRole.DevOpsEngineer;

    public bool CanManageRetention(UserRole role)
        => role is UserRole.Owner or UserRole.Admin;

    public bool CanViewMetrics(UserRole role)
        => true;

    public bool CanManageAlerts(UserRole role)
        => role is UserRole.Owner or UserRole.Admin or UserRole.DevOpsEngineer;
}
