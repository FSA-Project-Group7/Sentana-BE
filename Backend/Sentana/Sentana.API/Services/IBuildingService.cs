using System.Security.Claims;
using Sentana.API.DTOs.Building;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public interface IBuildingService
    {
        Task<Building> CreateBuildingAsync(CreateBuildingDto dto, ClaimsPrincipal user);

        Task<Building> UpdateBuildingAsync(int id, UpdateBuildingDto dto, ClaimsPrincipal user);

        Task<bool> DeleteBuildingAsync(int id, ClaimsPrincipal user);
    }
}

