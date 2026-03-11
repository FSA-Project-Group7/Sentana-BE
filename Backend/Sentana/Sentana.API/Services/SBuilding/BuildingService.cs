using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Building;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services.SBuilding
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
			// 1. Chốt chặn Validate tầng (Vì số căn hộ và mã phụ thuộc vào số tầng)
			if (!dto.FloorNumber.HasValue || dto.FloorNumber <= 0)
				throw new ArgumentException("Số tầng là bắt buộc và phải lớn hơn 0.");

			int? accountId = null;
			var accountIdClaim = user?.FindFirst("AccountId");
			if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
			{
				accountId = parsedAccountId;
			}

			// 2. LOGIC TỰ ĐỘNG SINH MÃ (Bỏ qua dto.BuildingCode và dto.BuildingName giả từ FE)
			var lastBuilding = await _context.Buildings
				.Where(b => b.BuildingCode != null && b.BuildingCode.StartsWith("SEN-"))
				.OrderByDescending(b => b.BuildingCode)
				.FirstOrDefaultAsync();

			char nextChar = 'A'; // Bắt đầu từ Tòa A nếu chưa có gì

			if (lastBuilding != null && lastBuilding.BuildingCode.Length >= 5)
			{
				char lastChar = lastBuilding.BuildingCode.Last();
				if (char.IsLetter(lastChar) && lastChar >= 'A' && lastChar < 'Z')
				{
					nextChar = (char)(lastChar + 1); // Tăng A -> B -> C
				}
				else if (lastChar == 'Z')
				{
					throw new InvalidOperationException("Hệ thống đã đạt giới hạn tối đa tòa nhà (Từ A đến Z).");
				}
			}

			string generatedCode = $"SEN-{nextChar}";
			string generatedName = $"Chung cư SENTANA Tòa {nextChar}";
			int calculatedApartmentNumber = dto.FloorNumber.Value * 10; // Tự tính số căn hộ

			// 3. Khởi tạo đối tượng lưu vào DB (Dùng dữ liệu đã tự sinh ra)
			var newBuilding = new Building
			{
				BuildingName = generatedName,
				BuildingCode = generatedCode,
				Address = dto.Address,
				City = dto.City,
				FloorNumber = dto.FloorNumber,
				ApartmentNumber = calculatedApartmentNumber,
				Status = GeneralStatus.Active,
				CreatedAt = DateTime.Now,
				IsDeleted = false,
				CreatedBy = accountId ?? 0
			};

			_context.Buildings.Add(newBuilding);
			await _context.SaveChangesAsync();

			return MapToResponseDto(newBuilding);
		}

		public async Task<BuildingResponseDto> UpdateBuildingAsync(int id, BuildingRequestDto dto, ClaimsPrincipal user)
		{
			int? accountId = null;
			var accountIdClaim = user?.FindFirst("AccountId");
			if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
				accountId = parsedAccountId;

			var existingBuilding = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);
			if (existingBuilding == null) throw new InvalidOperationException("Không tìm thấy tòa nhà.");

			// Giữ nguyên Tên và Mã (Vì FE gửi lại cái cũ để vượt qua Validate)
			if (!string.IsNullOrWhiteSpace(dto.BuildingName) && dto.BuildingName != existingBuilding.BuildingName)
				existingBuilding.BuildingName = dto.BuildingName;

			if (!string.IsNullOrWhiteSpace(dto.BuildingCode) && dto.BuildingCode != existingBuilding.BuildingCode)
				existingBuilding.BuildingCode = dto.BuildingCode;

			// 1. Cập nhật Địa chỉ
			if (dto.Address != null) existingBuilding.Address = dto.Address;
			if (dto.City != null) existingBuilding.City = dto.City;

			// 2. Cập nhật Tầng và TỰ ĐỘNG tính lại số căn hộ
			if (dto.FloorNumber.HasValue)
			{
				existingBuilding.FloorNumber = dto.FloorNumber.Value;
				existingBuilding.ApartmentNumber = dto.FloorNumber.Value * 10;
			}

			// 2. Ép Trạng thái nhận giá trị từ giao diện
			if (dto.Status.HasValue)
			{
				existingBuilding.Status = (GeneralStatus)dto.Status.Value;
			}

			if (!string.IsNullOrEmpty(dto.Address)) existingBuilding.Address = dto.Address;
			if (!string.IsNullOrEmpty(dto.City)) existingBuilding.City = dto.City;

			existingBuilding.UpdatedAt = DateTime.UtcNow;

			// === KẾT THÚC ĐOẠN ÉP KIỂU ===

			_context.Buildings.Update(existingBuilding);
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
				Status = (byte?)building.Status
			};
        }
    }
}
