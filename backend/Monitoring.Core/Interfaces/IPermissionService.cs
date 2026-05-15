using Monitoring.Core.Enums;

namespace Monitoring.Core.Interfaces;

public interface IPermissionService
{
    bool HasPermission(UserRole role, string permission);
    bool CanManageUsers(UserRole role);
    bool CanManageWorkspace(UserRole role);
    bool CanConnectRepos(UserRole role);
    bool CanConnectAgents(UserRole role);
    bool CanConfigureAlerts(UserRole role);
    bool CanResolveAlerts(UserRole role);
    bool CanManageRetention(UserRole role);
    bool CanViewMetrics(UserRole role);
    bool CanManageAlerts(UserRole role);
}
