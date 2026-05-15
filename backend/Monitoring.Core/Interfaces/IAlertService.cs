using Monitoring.Core.DTOs;

namespace Monitoring.Core.Interfaces;

public interface IAlertService
{
    Task<PagedResult<AlertDto>> GetAlertsAsync(Guid workspaceId, AlertListRequest request);
    Task<AlertDto?> GetAlertByIdAsync(Guid alertId, Guid workspaceId);
    Task<AlertRuleDto> CreateAlertRuleAsync(Guid workspaceId, CreateAlertRuleRequest request);
    Task<AlertRuleDto> UpdateAlertRuleAsync(Guid ruleId, Guid workspaceId, UpdateAlertRuleRequest request);
    Task<bool> DeleteAlertRuleAsync(Guid ruleId, Guid workspaceId);
    Task<List<AlertRuleDto>> GetAlertRulesAsync(Guid workspaceId);
    Task<bool> AcknowledgeAlertAsync(Guid alertId, Guid userId, Guid workspaceId);
    Task<bool> ResolveAlertAsync(Guid alertId, Guid userId, Guid workspaceId);
    Task<bool> MuteAlertAsync(Guid alertId, Guid workspaceId);
}
