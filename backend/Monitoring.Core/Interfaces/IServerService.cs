using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;

namespace Monitoring.Core.Interfaces;

public interface IServerService
{
    Task<Server> RegisterServerAsync(Guid workspaceId, RegisterServerRequest request);
    Task<List<Server>> GetServersAsync(Guid workspaceId);
    Task<Server?> GetServerAsync(Guid serverId);
    Task UpdateServerAsync(Guid serverId, RegisterServerRequest request);
    Task DeleteServerAsync(Guid serverId);
    Task ProcessHeartbeatAsync(Guid serverId);
    Task ProcessMetricsAsync(Guid serverId, SystemMetricsDto metrics);
    Task<ServerHealthDto> GetServerHealthAsync(Guid serverId);
}
