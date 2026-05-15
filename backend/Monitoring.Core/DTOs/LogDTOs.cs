namespace Monitoring.Core.DTOs;

public record LogSearchRequest(
    string? Keyword,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? SourceType,
    string? Severity,
    int Page = 1,
    int PageSize = 50
);

public record LogSearchResult(List<LogEntryDto> Items, int Total, int Page, int PageSize);

public record LogEntryDto(
    Guid Id,
    string SourceType,
    string? SourceName,
    DateTimeOffset Timestamp,
    string Level,
    string Message
);
