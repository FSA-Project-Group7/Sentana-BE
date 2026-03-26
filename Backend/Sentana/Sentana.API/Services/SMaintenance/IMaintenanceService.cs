using Sentana.API.DTOs.Maintenance;

namespace Sentana.API.Services.SMaintenance
{
    public interface IMaintenanceService
    {
        Task<(bool IsSuccess, string Message, object? Data)> GetMyAssignedTasksAsync(int currentTechId, int pageIndex = 1, int pageSize = 10);
        Task<(bool IsSuccess, string Message)> AcceptTaskAsync(int requestId, int currentTechId);
        Task<(bool IsSuccess, string Message)> StartProcessingTaskAsync(int requestId, int currentTechId);
        Task<(bool IsSuccess, string Message)> FixTaskAsync(int requestId, FixTaskRequestDto request, int currentTechId);
    }
}
