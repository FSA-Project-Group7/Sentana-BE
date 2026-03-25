using System.Security.Claims;
using Sentana.API.DTOs.Building;
using Sentana.API.Models;

namespace Sentana.API.Services.SBuilding
{
    public interface IBuildingService
    {
        Task<BuildingResponseDto> CreateBuildingAsync(BuildingRequestDto dto, int managerId);

        Task<BuildingResponseDto> UpdateBuildingAsync(int id, BuildingRequestDto dto, int managerId);

        Task<bool> DeleteBuildingAsync(int id, ClaimsPrincipal user);
		Task<IEnumerable<BuildingResponseDto>> GetDeletedBuildingsAsync(int? managerId = null);
		Task<bool> RestoreBuildingAsync(int id);
		Task<bool> HardDeleteBuildingAsync(int id);
        Task<IEnumerable<BuildingResponseDto>> GetBuildingListAsync(int? managerId = null);
    }
}

