using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class BuildingService : IBuildingService
    {
        private readonly SentanaContext _context;

        public BuildingService(SentanaContext context)
        {
            _context = context;
        }

        public async Task<Building> CreateBuildingAsync(Building newBuilding, int? accountId)
        {
            if (newBuilding == null || string.IsNullOrWhiteSpace(newBuilding.BuildingName))
            {
                throw new ArgumentException("Tên tòa nhà là bắt buộc.");
            }

            var isNameExists = await _context.Buildings
                .AnyAsync(b => b.BuildingName == newBuilding.BuildingName && b.IsDeleted == false);

            if (isNameExists)
            {
                throw new InvalidOperationException("Tên tòa nhà đã tồn tại.");
            }

            newBuilding.CreatedAt = DateTime.Now;
            newBuilding.IsDeleted = false;

            if (accountId.HasValue)
            {
                newBuilding.CreatedBy = accountId.Value;
            }

            _context.Buildings.Add(newBuilding);
            await _context.SaveChangesAsync();

            return newBuilding;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;

namespace Sentana.API.Services;

public class BuildingService : IBuildingService
{
    private readonly SentanaContext _context;

    public BuildingService(SentanaContext context)
    {
        _context = context;
    }

    public async Task<Building> CreateBuildingAsync(Building newBuilding, int? accountId)
    {
        if (newBuilding == null || string.IsNullOrWhiteSpace(newBuilding.BuildingName))
        {
            throw new ArgumentException("Tên tòa nhà là bắt buộc.");
        }

        var isNameExists = await _context.Buildings
            .AnyAsync(b => b.BuildingName == newBuilding.BuildingName && b.IsDeleted == false);

        if (isNameExists)
        {
            throw new InvalidOperationException("Tên tòa nhà đã tồn tại.");
        }

        newBuilding.CreatedAt = DateTime.Now;
        newBuilding.IsDeleted = false;

        if (accountId.HasValue)
        {
            newBuilding.CreatedBy = accountId.Value;
        }

        _context.Buildings.Add(newBuilding);
        await _context.SaveChangesAsync();

        return newBuilding;
    }
}

