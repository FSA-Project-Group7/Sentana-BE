using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Building;
using Sentana.API.Enums;
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

        public async Task<BuildingResponseDto> CreateBuildingAsync(BuildingRequestDto dto, ClaimsPrincipal user)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.BuildingName))
            {
                throw new ArgumentException("Tên tòa nhà là bắt buộc.");
            }

            if (string.IsNullOrWhiteSpace(dto.BuildingCode))
            {
                throw new ArgumentException("Mã tòa nhà là bắt buộc.");
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

            var isCodeExists = await _context.Buildings
                .AnyAsync(b => b.BuildingCode == dto.BuildingCode && b.IsDeleted == false);

            if (isCodeExists)
            {
                throw new InvalidOperationException("Mã tòa nhà đã tồn tại.");
            }

            var newBuilding = new Building
            {
                BuildingName = dto.BuildingName,
                BuildingCode = dto.BuildingCode,
                Address = dto.Address,
                City = dto.City,
                FloorNumber = dto.FloorNumber,
                ApartmentNumber = dto.ApartmentNumber,
                Status = GeneralStatus.Active,
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };

            if (accountId.HasValue)
            {
                newBuilding.CreatedBy = accountId.Value;
            }

            _context.Buildings.Add(newBuilding);
            await _context.SaveChangesAsync();

            return MapToResponseDto(newBuilding);
        }

        public async Task<BuildingResponseDto> UpdateBuildingAsync(int id, BuildingRequestDto dto, ClaimsPrincipal user)
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
                if (string.IsNullOrWhiteSpace(dto.BuildingCode))
                    throw new ArgumentException("Mã tòa nhà không được để trống.");

                var isCodeExists = await _context.Buildings
                    .AnyAsync(b => b.BuildingCode == dto.BuildingCode
                                   && b.BuildingId != id
                                   && b.IsDeleted == false);

                if (isCodeExists)
                    throw new InvalidOperationException("Mã tòa nhà đã tồn tại.");

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

            existingBuilding.UpdatedAt = DateTime.UtcNow;
            existingBuilding.UpdatedBy = accountId;

            await _context.SaveChangesAsync();

            return MapToResponseDto(existingBuilding);
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

            // không cho xóa nếu tòa nhà còn căn hộ
            var hasApartments = await _context.Apartments
                .AnyAsync(a => a.BuildingId == id && a.IsDeleted == false);

            if (hasApartments)
                throw new InvalidOperationException("Tòa nhà đang chứa căn hộ, không thể xóa.");

            existingBuilding.IsDeleted = true;
            existingBuilding.UpdatedAt = DateTime.UtcNow;
            existingBuilding.UpdatedBy = accountId;

            await _context.SaveChangesAsync();

            return true;
        }

        private static BuildingResponseDto MapToResponseDto(Building building)
        {
            return new BuildingResponseDto
            {
                BuildingId = building.BuildingId,
                BuildingName = building.BuildingName,
                BuildingCode = building.BuildingCode,
                Address = building.Address,
                City = building.City,
                FloorNumber = building.FloorNumber,
                ApartmentNumber = building.ApartmentNumber,
                StatusName = building.Status?.ToString() ?? string.Empty
            };
        }
    }
}
