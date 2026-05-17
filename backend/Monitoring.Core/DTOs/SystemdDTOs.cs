namespace Monitoring.Core.DTOs;

public record AgentSystemdRequest(Guid ServerId, List<AgentSystemdServiceDto> Services, DateTimeOffset? CollectedAt);

public record AgentSystemdServiceDto(
    string Name,
    string? DisplayName,
    string LoadState,
    string ActiveState,
    string SubState,
    string? Description,
    string? FragmentPath,
    int? MainPid,
    long? MemoryCurrent,
    long? CpuUsageNSec,
    int? RestartCount
);

public record AgentSystemdLogsRequest(Guid ServerId, List<AgentSystemdLogEntry> Logs);

public record AgentSystemdLogEntry(
    string UnitName,
    DateTimeOffset? Timestamp,
    int? Priority,
    string? Level,
    string Message,
    string? RawJson,
    string? Cursor,
    string? BootId
);

public record SystemdServiceDto(
    Guid Id,
    Guid ServerId,
    string Name,
    string? DisplayName,
    string LoadState,
    string ActiveState,
    string SubState,
    string? Description,
    string? FragmentPath,
    int? MainPid,
    long? MemoryCurrent,
    long? CpuUsageNSec,
    int? RestartCount,
    DateTime UpdatedAt
);

public record SystemdLogDto(Guid Id, DateTimeOffset Timestamp, int? Priority, string Level, string Message, string? Cursor, string? BootId);
