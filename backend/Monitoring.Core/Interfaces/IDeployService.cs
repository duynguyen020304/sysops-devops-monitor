namespace Monitoring.Core.Interfaces;

public interface IDeployService
{
    Task<DeployResult> DeployAgentAsync(Guid serverId, CancellationToken ct = default);
}

public record DeployResult(bool Success, string Output, string? Error);
