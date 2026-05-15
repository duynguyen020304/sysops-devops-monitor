namespace Monitoring.Core.DTOs;

public record DeployAgentResponse(bool Success, string Output, string? Error, DateTimeOffset DeployedAt);
