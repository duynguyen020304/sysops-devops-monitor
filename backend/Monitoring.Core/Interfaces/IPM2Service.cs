using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;

namespace Monitoring.Core.Interfaces;

public interface IPM2Service
{
    Task ProcessPM2DataAsync(Guid serverId, List<PM2ProcessDto> processes);
    Task<List<PM2Process>> GetProcessesByServerAsync(Guid serverId);
    Task<PM2Process?> GetProcessAsync(Guid processId);
    Task<List<PM2Log>> GetProcessLogsAsync(Guid processId, int limit = 100);
}
