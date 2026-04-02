using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Maintenance;
using System.Threading.Tasks;

namespace Sentana.API.Services.SMaintenance
{
    public interface IMaintenanceService
    {
        Task<(bool IsSuccess, string Message, object? Data)> GetMyAssignedTasksAsync(int currentTechId, int pageIndex = 1, int pageSize = 10);
        Task<(bool IsSuccess, string Message)> AcceptTaskAsync(int requestId, int currentTechId);
        Task<(bool IsSuccess, string Message)> StartProcessingTaskAsync(int requestId, int currentTechId);
        Task<(bool IsSuccess, string Message)> FixTaskAsync(int requestId, FixTaskRequestDto request, int currentTechId);

        Task<(bool IsSuccess, string Message, object? Data)> CreateResidentRequestAsync(CreateMaintenanceDto request, int residentId);
        Task<(bool IsSuccess, string Message, object? Data)> GetResidentRequestsAsync(int residentId);
        Task<(bool IsSuccess, string Message, object? Data)> GetMyActiveApartmentsAsync(int residentId);
        
        // Code từ nhánh của bạn (Lấy danh mục sự cố)
        Task<(bool IsSuccess, string Message, object? Data)> GetIssueCategoriesAsync();

        Task<PagedResult<MaintenanceResponseDto>> GetRequestsForManagerAsync(int managerId, int pageIndex = 1, int pageSize = 10);

        Task<bool> AssignTechnicianAsync(int requestId, int managerId, AssignMaintenanceRequestDto dto);
        Task<(bool IsSuccess, string Message)> CloseTaskAsync(int requestId, int managerId);
        Task<(bool IsSuccess, string Message)> RejectTaskAsync(int requestId, RejectTaskRequestDto request, int managerId);

        // US18 - View Maintenance History (Resident)
        Task<List<MaintenanceHistoryDto>> GetMyMaintenanceHistoryAsync(int residentId);

        // US19 - Track Maintenance Status (Resident)
        Task<MaintenanceStatusDto?> GetMaintenanceStatusAsync(int requestId, int residentId);
    }
}