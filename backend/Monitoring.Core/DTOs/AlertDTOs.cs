namespace Monitoring.Core.DTOs;

public record AlertDto(
    Guid Id,
    string SourceType,
    Guid? SourceId,
    string Title,
    string Description,
    string Severity,
    string Status,
    DateTimeOffset TriggeredAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    Guid? AssignedUserId,
    Guid? RuleId,
    string? RuleName
);

public record AlertRuleDto(
    Guid Id,
    string Name,
    string SourceType,
    string ConditionType,
    double Threshold,
    int TimeWindowSeconds,
    string Severity,
    bool IsEnabled,
    int CooldownSeconds
);

public record CreateAlertRuleRequest(
    string Name,
    string SourceType,
    string ConditionType,
    double Threshold,
    int TimeWindowSeconds,
    string Severity,
    bool IsEnabled,
    int CooldownSeconds
);

public record UpdateAlertRuleRequest(
    string? Name,
    string? SourceType,
    string? ConditionType,
    double? Threshold,
    int? TimeWindowSeconds,
    string? Severity,
    bool? IsEnabled,
    int? CooldownSeconds
);

public record AlertListRequest(
    string? Status,
    string? Severity,
    string? SourceType,
    int Page = 1,
    int PageSize = 20
);
