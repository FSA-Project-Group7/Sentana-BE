using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Building;
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

        public async Task<Building> CreateBuildingAsync(CreateBuildingDto dto, ClaimsPrincipal user)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.BuildingName))
            {
                throw new ArgumentException("Tên tòa nhà là bắt buộc.");
            }

            int? accountId = null;
            var accountIdClaim = user?.FindFirst("AccountId");
            if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
            {
                accountId = parsedAccountId;
            }

            var isNameExists = await _context.Buildings
                .AnyAsync(b => b.BuildingName == dto.BuildingName && b.IsDeleted == false);

            if (isNameExists)
            {
                throw new InvalidOperationException("Tên tòa nhà đã tồn tại.");
            }

            var newBuilding = new Building
            {
                BuildingName = dto.BuildingName,
                BuildingCode = dto.BuildingCode,
                Address = dto.Address,
                City = dto.City,
                FloorNumber = dto.FloorNumber,
                ApartmentNumber = dto.ApartmentNumber,
                Status = dto.Status,
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };

            if (accountId.HasValue)
            {
                newBuilding.CreatedBy = accountId.Value;
            }

            _context.Buildings.Add(newBuilding);
            await _context.SaveChangesAsync();

            return newBuilding;
        }

        public async Task<Building> UpdateBuildingAsync(int id, UpdateBuildingDto dto, ClaimsPrincipal user)
        {
            int? accountId = null;
            var accountIdClaim = user?.FindFirst("AccountId");
            if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
            {
                accountId = parsedAccountId;
            }

            var existingBuilding = await _context.Buildings
                .FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);

            if (existingBuilding == null)
                throw new InvalidOperationException("Không tìm thấy tòa nhà.");

            if (!string.IsNullOrWhiteSpace(dto.BuildingName))
            {
                var isNameExists = await _context.Buildings
                    .AnyAsync(b => b.BuildingName == dto.BuildingName
                                   && b.BuildingId != id
                                   && b.IsDeleted == false);

                if (isNameExists)
                    throw new InvalidOperationException("Tên tòa nhà đã tồn tại.");

                existingBuilding.BuildingName = dto.BuildingName;
            }

            if (dto.BuildingCode != null)
            {
                existingBuilding.BuildingCode = dto.BuildingCode;
            }

            if (dto.Address != null)
            {
                existingBuilding.Address = dto.Address;
            }

            if (dto.City != null)
            {
                existingBuilding.City = dto.City;
            }

            if (dto.FloorNumber.HasValue)
            {
                existingBuilding.FloorNumber = dto.FloorNumber.Value;
            }

            if (dto.ApartmentNumber.HasValue)
            {
                existingBuilding.ApartmentNumber = dto.ApartmentNumber.Value;
            }

            if (dto.Status.HasValue)
            {
                existingBuilding.Status = dto.Status.Value;
            }

            existingBuilding.UpdatedAt = DateTime.UtcNow;
            existingBuilding.UpdatedBy = accountId;

            await _context.SaveChangesAsync();

            return existingBuilding;
        }

        public async Task<bool> DeleteBuildingAsync(int id, ClaimsPrincipal user)
        {
            int? accountId = null;
            var accountIdClaim = user?.FindFirst("AccountId");
            if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
            {
                accountId = parsedAccountId;
            }

            var existingBuilding = await _context.Buildings
                .FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);

            if (existingBuilding == null)
                throw new InvalidOperationException("Không tìm thấy tòa nhà.");

            existingBuilding.IsDeleted = true;
            existingBuilding.UpdatedAt = DateTime.UtcNow;
            existingBuilding.UpdatedBy = accountId;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}

