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
			if (!dto.FloorNumber.HasValue || dto.FloorNumber <= 0)
				throw new ArgumentException("Số tầng là bắt buộc và phải lớn hơn 0.");

			int? accountId = null;
			var accountIdClaim = user?.FindFirst("AccountId");
			if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
			{
				accountId = parsedAccountId;
			}

			var lastBuilding = await _context.Buildings
				.Where(b => b.BuildingCode != null && b.BuildingCode.StartsWith("SEN-"))
				.OrderByDescending(b => b.BuildingCode)
				.FirstOrDefaultAsync();

			char nextChar = 'A'; 

			if (lastBuilding != null && lastBuilding.BuildingCode.Length >= 5)
			{
				char lastChar = lastBuilding.BuildingCode.Last();
				if (char.IsLetter(lastChar) && lastChar >= 'A' && lastChar < 'Z')
				{
					nextChar = (char)(lastChar + 1);
				}
				else if (lastChar == 'Z')
				{
					throw new InvalidOperationException("Hệ thống đã đạt giới hạn tối đa tòa nhà (Từ A đến Z).");
				}
			}

			string generatedCode = $"SEN-{nextChar}";
			string generatedName = $"Chung cư SENTANA Tòa {nextChar}";
			int calculatedApartmentNumber = dto.FloorNumber.Value * 10;

			var newBuilding = new Building
			{
				BuildingName = generatedName,
				BuildingCode = generatedCode,
				Address = dto.Address,
				City = dto.City,
				FloorNumber = dto.FloorNumber,
				ApartmentNumber = calculatedApartmentNumber,
				Status = (GeneralStatus)1, 
				CreatedAt = DateTime.UtcNow,
				IsDeleted = false,
				CreatedBy = accountId ?? 0
			};

			_context.Buildings.Add(newBuilding);
			await _context.SaveChangesAsync(); 
			var newApartments = new List<Apartment>();

			for (int floor = 1; floor <= newBuilding.FloorNumber; floor++)
			{
				for (int aptIndex = 1; aptIndex <= 10; aptIndex++) 
				{
					int roomNumber = (floor * 100) + aptIndex;

					newApartments.Add(new Apartment
					{
						BuildingId = newBuilding.BuildingId, 
						ApartmentCode = $"{newBuilding.BuildingCode}-{roomNumber}", 
						ApartmentName = $"Phòng {roomNumber} Tòa {nextChar}",       
						ApartmentNumber = roomNumber,
						FloorNumber = floor,
						Area = 0,
						Status = (ApartmentStatus)1, 
						CreatedAt = DateTime.UtcNow,
						CreatedBy = accountId ?? 0,
						IsDeleted = false
					});
				}
			}

			await _context.Apartments.AddRangeAsync(newApartments);
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
			var existingBuilding = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);
			if (existingBuilding == null) throw new InvalidOperationException("Không tìm thấy tòa nhà.");

			//Kiểm tra xem tòa nhà còn căn hộ nào không
			var hasActiveApartments = await _context.Apartments
				.AnyAsync(a => a.BuildingId == id && a.IsDeleted == false);

			if (hasActiveApartments)
				throw new InvalidOperationException("Không thể đưa vào thùng rác! Tòa nhà này vẫn đang có căn hộ tồn tại bên trong.");

			existingBuilding.IsDeleted = true;
			existingBuilding.UpdatedAt = DateTime.UtcNow;

			// Lấy ID của người Admin đang thao tác xóa để lưu vào DB
			var accountIdClaim = user?.FindFirst("AccountId");
			if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
			{
				existingBuilding.UpdatedBy = parsedAccountId;
			}

			_context.Buildings.Update(existingBuilding);
			await _context.SaveChangesAsync();
			return true;
		}

		//Lấy danh sách đã xóa mềm
		public async Task<IEnumerable<BuildingResponseDto>> GetDeletedBuildingsAsync()
		{
			return await _context.Buildings
				.Where(b => b.IsDeleted == true) // Chỉ lấy các tòa nhà đã xóa mềm
				.Select(b => new BuildingResponseDto
				{
					BuildingId = b.BuildingId,
					BuildingName = b.BuildingName,
					BuildingCode = b.BuildingCode,
					Address = b.Address,
					City = b.City,
					FloorNumber = b.FloorNumber,
					ApartmentNumber = b.ApartmentNumber,
					Status = (byte?)b.Status
				})
				.ToListAsync();
		}

		//Khôi phục tòa nhà đã xóa mềm
		public async Task<bool> RestoreBuildingAsync(int id)
		{
			var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == true);
			if (building == null) throw new InvalidOperationException("Không tìm thấy tòa nhà trong thùng rác.");

			building.IsDeleted = false; // Khôi phục lại
			building.UpdatedAt = DateTime.UtcNow;

			_context.Buildings.Update(building);
			await _context.SaveChangesAsync();
			return true;
		}

		//Xóa vĩnh viễn
		public async Task<bool> HardDeleteBuildingAsync(int id)
		{
			var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == true);
			if (building == null) throw new InvalidOperationException("Không tìm thấy tòa nhà trong thùng rác.");

			// Kiểm tra lại một lần nữa cho chắc chắn trước khi xóa bay màu
			var hasActiveApartments = await _context.Apartments
				.AnyAsync(a => a.BuildingId == id && a.IsDeleted == false);

			if (hasActiveApartments)
				throw new InvalidOperationException("Không thể xóa vĩnh viễn! Tòa nhà này vẫn đang chứa các căn hộ chưa bị xóa.");

			_context.Buildings.Remove(building);
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
