using System.Security.Claims;
using Sentana.API.DTOs.Building;
using Sentana.API.Models;

namespace Sentana.API.Services.SBuilding
{
    public interface IBuildingService
    {
        Task<BuildingResponseDto> CreateBuildingAsync(BuildingRequestDto dto, ClaimsPrincipal user);

        Task<BuildingResponseDto> UpdateBuildingAsync(int id, BuildingRequestDto dto, ClaimsPrincipal user);

        Task<bool> DeleteBuildingAsync(int id, ClaimsPrincipal user);
		Task<IEnumerable<BuildingResponseDto>> GetDeletedBuildingsAsync();
		Task<bool> RestoreBuildingAsync(int id);
		Task<bool> HardDeleteBuildingAsync(int id);
	}
}

