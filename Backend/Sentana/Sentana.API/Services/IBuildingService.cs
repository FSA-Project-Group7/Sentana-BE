using Sentana.API.DTOs.Building;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public interface IBuildingService
    {
        Task<Building> CreateBuildingAsync(Building newBuilding, int? accountId);

        Task<Building> UpdateBuildingAsync(int id, UpdateBuildingDto dto, int? accountId);
    }
}

