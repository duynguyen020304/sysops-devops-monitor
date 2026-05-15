using Microsoft.EntityFrameworkCore;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class AlertService : IAlertService
{
    private readonly MonitoringDbContext _db;

    public AlertService(MonitoringDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AlertDto>> GetAlertsAsync(Guid workspaceId, AlertListRequest request)
    {
        var query = _db.Alerts
            .Where(a => a.WorkspaceId == workspaceId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<AlertStatus>(request.Status, true, out var status))
            query = query.Where(a => a.Status == status);

        if (!string.IsNullOrEmpty(request.Severity) && Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
            query = query.Where(a => a.Severity == severity);

        if (!string.IsNullOrEmpty(request.SourceType) && Enum.TryParse<AlertSourceType>(request.SourceType, true, out var sourceType))
            query = query.Where(a => a.SourceType == sourceType);

        var total = await query.CountAsync();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var alerts = await query
            .OrderByDescending(a => a.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var ruleIds = alerts.Where(a => a.RuleId.HasValue).Select(a => a.RuleId!.Value).Distinct().ToList();
        var ruleNames = await _db.AlertRules
            .Where(r => ruleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name);

        var dtos = alerts.Select(a => new AlertDto(
            Id: a.Id,
            SourceType: a.SourceType.ToString(),
            SourceId: a.SourceId,
            Title: a.Title,
            Description: a.Description,
            Severity: a.Severity.ToString(),
            Status: a.Status.ToString(),
            TriggeredAt: a.TriggeredAt,
            AcknowledgedAt: a.AcknowledgedAt,
            ResolvedAt: a.ResolvedAt,
            AssignedUserId: a.AssignedUserId,
            RuleId: a.RuleId,
            RuleName: a.RuleId.HasValue && ruleNames.TryGetValue(a.RuleId.Value, out var name) ? name : null
        )).ToList();

        return new PagedResult<AlertDto>(dtos, total, page, pageSize);
    }

    public async Task<AlertDto?> GetAlertByIdAsync(Guid alertId, Guid workspaceId)
    {
        var alert = await _db.Alerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.WorkspaceId == workspaceId);

        if (alert is null)
            return null;

        string? ruleName = null;
        if (alert.RuleId.HasValue)
        {
            ruleName = await _db.AlertRules
                .Where(r => r.Id == alert.RuleId.Value)
                .Select(r => r.Name)
                .FirstOrDefaultAsync();
        }

        return new AlertDto(
            Id: alert.Id,
            SourceType: alert.SourceType.ToString(),
            SourceId: alert.SourceId,
            Title: alert.Title,
            Description: alert.Description,
            Severity: alert.Severity.ToString(),
            Status: alert.Status.ToString(),
            TriggeredAt: alert.TriggeredAt,
            AcknowledgedAt: alert.AcknowledgedAt,
            ResolvedAt: alert.ResolvedAt,
            AssignedUserId: alert.AssignedUserId,
            RuleId: alert.RuleId,
            RuleName: ruleName
        );
    }

    public async Task<AlertRuleDto> CreateAlertRuleAsync(Guid workspaceId, CreateAlertRuleRequest request)
    {
        if (!Enum.TryParse<AlertSourceType>(request.SourceType, true, out var sourceType))
            throw new ArgumentException($"Invalid source type: {request.SourceType}");

        if (!Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
            throw new ArgumentException($"Invalid severity: {request.Severity}");

        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = request.Name,
            SourceType = sourceType,
            ConditionType = request.ConditionType,
            Threshold = request.Threshold,
            TimeWindowSeconds = request.TimeWindowSeconds,
            Severity = severity,
            IsEnabled = request.IsEnabled,
            CooldownSeconds = request.CooldownSeconds,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.AlertRules.Add(rule);
        await _db.SaveChangesAsync();

        return MapRuleToDto(rule);
    }

    public async Task<AlertRuleDto> UpdateAlertRuleAsync(Guid ruleId, Guid workspaceId, UpdateAlertRuleRequest request)
    {
        var rule = await _db.AlertRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.WorkspaceId == workspaceId)
            ?? throw new InvalidOperationException("Alert rule not found.");

        if (request.Name is not null)
            rule.Name = request.Name;

        if (request.SourceType is not null)
        {
            if (!Enum.TryParse<AlertSourceType>(request.SourceType, true, out var sourceType))
                throw new ArgumentException($"Invalid source type: {request.SourceType}");
            rule.SourceType = sourceType;
        }

        if (request.ConditionType is not null)
            rule.ConditionType = request.ConditionType;

        if (request.Threshold.HasValue)
            rule.Threshold = request.Threshold.Value;

        if (request.TimeWindowSeconds.HasValue)
            rule.TimeWindowSeconds = request.TimeWindowSeconds.Value;

        if (request.Severity is not null)
        {
            if (!Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
                throw new ArgumentException($"Invalid severity: {request.Severity}");
            rule.Severity = severity;
        }

        if (request.IsEnabled.HasValue)
            rule.IsEnabled = request.IsEnabled.Value;

        if (request.CooldownSeconds.HasValue)
            rule.CooldownSeconds = request.CooldownSeconds.Value;

        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return MapRuleToDto(rule);
    }

    public async Task<bool> DeleteAlertRuleAsync(Guid ruleId, Guid workspaceId)
    {
        var rule = await _db.AlertRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.WorkspaceId == workspaceId);

        if (rule is null)
            return false;

        _db.AlertRules.Remove(rule);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<AlertRuleDto>> GetAlertRulesAsync(Guid workspaceId)
    {
        var rules = await _db.AlertRules
            .Where(r => r.WorkspaceId == workspaceId)
            .OrderBy(r => r.Name)
            .ToListAsync();

        return rules.Select(MapRuleToDto).ToList();
    }

    public async Task<bool> AcknowledgeAlertAsync(Guid alertId, Guid userId, Guid workspaceId)
    {
        var alert = await _db.Alerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.WorkspaceId == workspaceId);

        if (alert is null || alert.Status != AlertStatus.Triggered)
            return false;

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        alert.AssignedUserId = userId;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResolveAlertAsync(Guid alertId, Guid userId, Guid workspaceId)
    {
        var alert = await _db.Alerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.WorkspaceId == workspaceId);

        if (alert is null || alert.Status == AlertStatus.Resolved)
            return false;

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTimeOffset.UtcNow;
        alert.AssignedUserId = userId;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MuteAlertAsync(Guid alertId, Guid workspaceId)
    {
        var alert = await _db.Alerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.WorkspaceId == workspaceId);

        if (alert is null || alert.Status == AlertStatus.Resolved)
            return false;

        alert.Status = AlertStatus.Muted;

        await _db.SaveChangesAsync();
        return true;
    }

    private static AlertRuleDto MapRuleToDto(AlertRule rule)
    {
        return new AlertRuleDto(
            Id: rule.Id,
            Name: rule.Name,
            SourceType: rule.SourceType.ToString(),
            ConditionType: rule.ConditionType,
            Threshold: rule.Threshold,
            TimeWindowSeconds: rule.TimeWindowSeconds,
            Severity: rule.Severity.ToString(),
            IsEnabled: rule.IsEnabled,
            CooldownSeconds: rule.CooldownSeconds
        );
    }
}
