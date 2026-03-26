using Sentana.API.DTOs.Maintenance;

namespace Sentana.API.Services.SMaintenance
{
    public interface IMaintenanceService
    {
        Task<(bool IsSuccess, string Message, List<MaintenanceTaskDto>? Data)> GetMyAssignedTasksAsync(int currentTechId);
        Task<(bool IsSuccess, string Message)> AcceptTaskAsync(int requestId, int currentTechId);
        Task<(bool IsSuccess, string Message)> StartProcessingTaskAsync(int requestId, int currentTechId);
        Task<(bool IsSuccess, string Message)> FixTaskAsync(int requestId, FixTaskRequestDto request, int currentTechId);
    }
}
