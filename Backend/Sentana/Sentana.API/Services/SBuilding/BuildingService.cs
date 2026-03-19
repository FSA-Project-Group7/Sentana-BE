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
						ApartmentName = $"Phòng {roomNumber} Chung cư SENTANA Tòa {nextChar}",       
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

			var existingBuilding = await _context.Buildings
				.Include(b => b.Apartments)
					.ThenInclude(a => a.Contracts) 
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);

			if (existingBuilding == null) throw new InvalidOperationException("Không tìm thấy tòa nhà.");

			if (!string.IsNullOrWhiteSpace(dto.BuildingName) && dto.BuildingName != existingBuilding.BuildingName)
				existingBuilding.BuildingName = dto.BuildingName;

			if (!string.IsNullOrWhiteSpace(dto.BuildingCode) && dto.BuildingCode != existingBuilding.BuildingCode)
				existingBuilding.BuildingCode = dto.BuildingCode;

			if (dto.Address != null) existingBuilding.Address = dto.Address;
			if (dto.City != null) existingBuilding.City = dto.City;

			// 2. Cập nhật Tầng và TỰ ĐỘNG tính lại số căn hộ
			if (dto.FloorNumber.HasValue)
			{
				existingBuilding.FloorNumber = dto.FloorNumber.Value;
				existingBuilding.ApartmentNumber = dto.FloorNumber.Value * 10;
			}

			// 2. Ép Trạng thái nhận giá trị từ giao diện
			if (dto.Status.HasValue && existingBuilding.Status != (GeneralStatus)dto.Status.Value)
			{
				var newStatus = (GeneralStatus)dto.Status.Value;
				existingBuilding.Status = newStatus;

				// Cập nhật trạng thái các căn hộ bên trong
				if (newStatus == GeneralStatus.Inactive) // Giả sử Inactive (0) hoặc 2 là Bảo trì (Tùy thuộc GeneralStatus của bạn)
				{
					// Nếu tòa nhà bảo trì -> Mọi phòng thành bảo trì (3)
					foreach (var apt in existingBuilding.Apartments.Where(a => a.IsDeleted == false))
					{
						apt.Status = ApartmentStatus.Maintenance;
					}
				}
				else if (newStatus == GeneralStatus.Active) // Tòa nhà hoạt động lại (1)
				{
					// Kiểm tra hợp đồng để quyết định phòng Trống (1) hay Đang ở (2)
					var today = DateOnly.FromDateTime(DateTime.Now);
					foreach (var apt in existingBuilding.Apartments.Where(a => a.IsDeleted == false))
					{
						bool hasActiveContract = apt.Contracts.Any(c => c.Status == GeneralStatus.Active && c.EndDay >= today);

						if (hasActiveContract)
						{
							apt.Status = ApartmentStatus.Occupied; 
						}
						else
						{
							apt.Status = ApartmentStatus.Vacant;  
						}
					}
				}
			}

			existingBuilding.UpdatedAt = DateTime.UtcNow;

			_context.Buildings.Update(existingBuilding);
			await _context.SaveChangesAsync();

			return MapToResponseDto(existingBuilding);
		}

		public async Task<bool> DeleteBuildingAsync(int id, ClaimsPrincipal user)
		{
			var existingBuilding = await _context.Buildings
				.Include(b => b.Apartments)
					.ThenInclude(a => a.Contracts)
				.Include(b => b.Apartments)
					.ThenInclude(a => a.ApartmentResidents)
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);

			if (existingBuilding == null) throw new InvalidOperationException("Không tìm thấy tòa nhà.");

			var activeApartments = existingBuilding.Apartments.Where(a => a.IsDeleted == false).ToList();

			bool hasOccupiedOrContractedApartments = activeApartments.Any(a =>
				a.Status != ApartmentStatus.Vacant ||
				a.Contracts.Any(c => c.IsDeleted == false) ||
				a.ApartmentResidents.Any(ar => ar.IsDeleted == false)
			);

			if (hasOccupiedOrContractedApartments)
				throw new InvalidOperationException("Không thể đưa vào thùng rác! Tòa nhà này đang có căn hộ có người ở, có hợp đồng hoặc có dữ liệu cư dân.");

			int? accountId = null;
			var accountIdClaim = user?.FindFirst("AccountId");
			if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
			{
				accountId = parsedAccountId;
			}

			existingBuilding.IsDeleted = true;
			existingBuilding.UpdatedAt = DateTime.UtcNow;
			existingBuilding.UpdatedBy = accountId;

			foreach (var apt in activeApartments)
			{
				apt.IsDeleted = true;
				apt.UpdatedAt = DateTime.UtcNow;
				apt.UpdatedBy = accountId;
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
			var building = await _context.Buildings
				.Include(b => b.Apartments)
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == true);

			if (building == null) throw new InvalidOperationException("Không tìm thấy tòa nhà trong thùng rác.");

			building.IsDeleted = false; 
			building.UpdatedAt = DateTime.UtcNow;

			foreach (var apt in building.Apartments.Where(a => a.IsDeleted == true))
			{
				apt.IsDeleted = false;
				apt.UpdatedAt = DateTime.UtcNow;
			}

			_context.Buildings.Update(building);
			await _context.SaveChangesAsync();
			return true;
		}

		//Xóa vĩnh viễn
		public async Task<bool> HardDeleteBuildingAsync(int id)
		{
			var building = await _context.Buildings
				.Include(b => b.Apartments)
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == true);

			if (building == null) throw new InvalidOperationException("Không tìm thấy tòa nhà trong thùng rác.");

			if (building.Apartments.Any())
			{
				_context.Apartments.RemoveRange(building.Apartments);
			}

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
