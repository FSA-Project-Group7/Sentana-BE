using Sentana.API.Models;

namespace Sentana.API.Services
{
    public interface IBuildingService
    {
        Task<Building> CreateBuildingAsync(Building newBuilding, int? accountId);
    }
}

