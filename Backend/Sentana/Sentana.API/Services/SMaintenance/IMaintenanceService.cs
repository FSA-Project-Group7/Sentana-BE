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

        // Code từ nhánh main của team (Lấy request cho Manager)
        Task<PagedResult<MaintenanceResponseDto>> GetRequestsForManagerAsync(int managerId, int pageIndex = 1, int pageSize = 10);
    }
}