using System.Security.Claims;
using Sentana.API.DTOs.Building;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public interface IBuildingService
    {
        Task<BuildingResponseDto> CreateBuildingAsync(BuildingRequestDto dto, ClaimsPrincipal user);

        Task<BuildingResponseDto> UpdateBuildingAsync(int id, BuildingRequestDto dto, ClaimsPrincipal user);

        Task<bool> DeleteBuildingAsync(int id, ClaimsPrincipal user);
    }
}

