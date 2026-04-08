using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Apartment;
using Sentana.API.Enums;
using Sentana.API.Models;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Sentana.API.Services.SApartment
{
    public class ApartmentService : IApartmentService
    {
        private readonly SentanaContext _context;

        public ApartmentService(SentanaContext context)
        {
            _context = context;
        }

		public async Task<IEnumerable<ApartmentDto>> GetApartmentListAsync(int managerId, int? buildingId = null)

        {
			var today = DateOnly.FromDateTime(DateTime.Now);
			var query =  _context.Apartments
				.Include(a => a.Contracts)
				.Include(a => a.Building)
				.Where(a => a.IsDeleted == false && a.Building.ManagerId == managerId);
                if (buildingId.HasValue && buildingId.Value > 0)
				{
					query = query.Where(a => a.BuildingId == buildingId.Value);
				}
				return await query
				.Select(a => new ApartmentDto
				{
					ApartmentId = a.ApartmentId,
					ApartmentCode = a.ApartmentCode,
					ApartmentName = a.ApartmentName,
					FloorNumber = a.FloorNumber,
					Area = a.Area,
					Status = (byte?)a.Status,
					HasTenant = a.Contracts.Any(c => c.Status == GeneralStatus.Active && c.EndDay >= today)
				})
				.ToListAsync();
		}

		public async Task<ApartmentDto> CreateApartmentAsync(CreateApartmentDto dto)
		{
			var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == dto.BuildingId && b.IsDeleted == false);
			if (building == null) throw new ArgumentException("Tòa nhà này không tồn tại hoặc đã bị xóa.");

			int floor = dto.FloorNumber;
			int room = dto.ApartmentNumber;

			if (room >= 100)
			{
				if (room / 100 == floor)
					room = room % 100; // Tự động cắt lấy số 9
				else
					throw new ArgumentException($"Số phòng ({dto.ApartmentNumber}) không khớp với cấu trúc của Tầng {floor}.");
			}

			if (building.ApartmentNumber.HasValue)
			{
				int currentRoomCount = await _context.Apartments.CountAsync(a => a.BuildingId == dto.BuildingId && a.IsDeleted == false);
				if (currentRoomCount >= building.ApartmentNumber.Value)
					throw new ArgumentException($"Tòa nhà {building.BuildingName} đã đạt số lượng tối đa ({building.ApartmentNumber.Value} phòng). Không thể tạo thêm.");
			}

			if (dto.FloorNumber > building.FloorNumber)
				throw new ArgumentException($"Tầng của căn hộ ({dto.FloorNumber}) không được vượt quá số tầng của tòa nhà ({building.FloorNumber}).");

			string generatedNumberStr = $"{floor}{room.ToString("00")}";
			int fullAppNumber = int.Parse(generatedNumberStr); 

			string generatedCode = $"{building.BuildingCode}-{generatedNumberStr}";

			string bName = building.BuildingName?.Trim() ?? "";
			string generatedName;

			if (bName.Contains("Chung cư", StringComparison.OrdinalIgnoreCase))
			{
				generatedName = $"Phòng {generatedNumberStr} {bName}";
			}
			else if (bName.StartsWith("Tòa", StringComparison.OrdinalIgnoreCase))
			{
				generatedName = $"Phòng {generatedNumberStr} Chung cư SENTANA {bName}";
			}
			else
			{
				generatedName = $"Phòng {generatedNumberStr} Chung cư SENTANA Tòa {bName}";
			}

			// 3. Validation: Kiểm tra trùng Mã và Số phòng
			bool isCodeExist = await _context.Apartments.AnyAsync(a => a.ApartmentCode == generatedCode && a.IsDeleted == false);
			if (isCodeExist) throw new ArgumentException($"Mã căn hộ '{generatedCode}' đã tồn tại trong hệ thống.");

			bool isAppNumberExist = await _context.Apartments.AnyAsync(a => a.BuildingId == dto.BuildingId && a.ApartmentNumber == fullAppNumber && a.IsDeleted == false);
			if (isAppNumberExist) throw new ArgumentException($"Số căn hộ {fullAppNumber} đã tồn tại trong tòa nhà này.");

			if (!Enum.IsDefined(typeof(ApartmentStatus), (ApartmentStatus)dto.Status))
				throw new ArgumentException("Trạng thái căn hộ không hợp lệ.");

			var newApartment = new Apartment
			{
				BuildingId = dto.BuildingId,
				ApartmentCode = generatedCode,
				ApartmentName = generatedName,
				ApartmentNumber = fullAppNumber,
				FloorNumber = floor,
				Area = dto.Area,
				Status = (ApartmentStatus)dto.Status,
				CreatedAt = DateTime.Now,
				IsDeleted = false
			};

			_context.Apartments.Add(newApartment);
			await _context.SaveChangesAsync();

			return new ApartmentDto
			{
				ApartmentId = newApartment.ApartmentId,
				ApartmentCode = newApartment.ApartmentCode,
				ApartmentName = newApartment.ApartmentName,
				FloorNumber = newApartment.FloorNumber,
				Area = newApartment.Area,
				Status = (byte)newApartment.Status!
			};
		}

		public async Task<bool> UpdateApartmentAsync(int id, UpdateApartmentDto dto)
		{
			var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);
			if (apartment == null) return false;

			var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == apartment.BuildingId && b.IsDeleted == false);

			if (dto.ApartmentNumber.HasValue)
			{
				int inputNumber = dto.ApartmentNumber.Value;
				int floor;
				int room;

				if (inputNumber >= 100)
				{
					floor = inputNumber / 100;
					room = inputNumber % 100;
				}
				else
				{
					floor = apartment.FloorNumber ?? 1;
					room = inputNumber;
				}

				if (building != null && building.FloorNumber.HasValue && floor > building.FloorNumber.Value)
					throw new ArgumentException($"Tầng phân tích được ({floor}) vượt quá số tầng của tòa nhà ({building.FloorNumber.Value}).");

				if (room <= 0 || room >= 100)
					throw new ArgumentException("Số phòng phân tích được không hợp lệ (phải từ 01 đến 99).");

				string generatedNumberStr = $"{floor}{room.ToString("00")}";
				int fullAppNumber = int.Parse(generatedNumberStr);
				string generatedCode = $"{building?.BuildingCode}-{generatedNumberStr}";

				string bName = building?.BuildingName?.Trim() ?? "";
				string generatedName;

				if (bName.Contains("Chung cư", StringComparison.OrdinalIgnoreCase))
				{
					generatedName = $"Phòng {generatedNumberStr} {bName}";
				}
				else if (bName.StartsWith("Tòa", StringComparison.OrdinalIgnoreCase))
				{
					generatedName = $"Phòng {generatedNumberStr} Chung cư SENTANA {bName}";
				}
				else
				{
					generatedName = $"Phòng {generatedNumberStr} Chung cư SENTANA Tòa {bName}";
				}

				// --- FIX 5: CHỐNG TỰ TRÙNG CHÍNH MÌNH (Đã bỏ qua ID của chính căn phòng đang sửa) ---
				bool isAppNumberExist = await _context.Apartments.AnyAsync(a => a.ApartmentId != id && a.BuildingId == apartment.BuildingId && a.ApartmentNumber == fullAppNumber && a.IsDeleted == false);
				if (isAppNumberExist) throw new ArgumentException($"Số căn hộ {fullAppNumber} đã tồn tại trong tòa nhà này.");

				bool isCodeExist = await _context.Apartments.AnyAsync(a => a.ApartmentId != id && a.ApartmentCode == generatedCode && a.IsDeleted == false);
				if (isCodeExist) throw new ArgumentException($"Mã căn hộ '{generatedCode}' đã được sử dụng cho phòng khác.");

				apartment.ApartmentNumber = fullAppNumber;
				apartment.FloorNumber = floor;
				apartment.ApartmentCode = generatedCode;
				apartment.ApartmentName = generatedName;
			}

			if (dto.Area.HasValue) apartment.Area = dto.Area.Value;

			if (dto.Status.HasValue)
			{
				if (!Enum.IsDefined(typeof(ApartmentStatus), (ApartmentStatus)dto.Status.Value))
					throw new ArgumentException("Trạng thái căn hộ không hợp lệ.");
				apartment.Status = (ApartmentStatus)dto.Status.Value;
			}

			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> UpdateStatusAsync(int id, byte newStatus)
		{
			if (!Enum.IsDefined(typeof(ApartmentStatus), (ApartmentStatus)newStatus))
				throw new ArgumentException("Trạng thái phòng không hợp lệ.");

			var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);
			if (apartment == null) return false;

			var requestedStatus = (ApartmentStatus)newStatus;

			var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == apartment.BuildingId && b.IsDeleted == false);
			bool isBuildingUnderMaintenance = building != null && (int?)building.Status == 3;

			bool hasActiveContract = await _context.Contracts
				.AnyAsync(c => c.ApartmentId == id && (int?)c.Status == 1 && c.IsDeleted == false);

			if (isBuildingUnderMaintenance && requestedStatus != (ApartmentStatus)3)
			{
				throw new ArgumentException($"Không thể đổi trạng thái vì toàn bộ Tòa nhà '{building?.BuildingName}' đang trong thời gian bảo trì.");
			}

			if (requestedStatus == (ApartmentStatus)1 && hasActiveContract)
			{
				throw new ArgumentException("Không thể chuyển thành phòng Trống vì phòng này đang có Hợp đồng thuê có hiệu lực!");
			}

			if (requestedStatus == (ApartmentStatus)2 && !hasActiveContract)
			{
				throw new ArgumentException("Không thể chuyển thành phòng Đang thuê vì hệ thống không ghi nhận Hợp đồng nào có hiệu lực cho phòng này.");
			}

			apartment.Status = requestedStatus;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return true;
		}

		private async Task SyncApartmentStatusAsync(Apartment apartment)
		{
			// 1. Kiểm tra trạng thái Tòa nhà (Giả sử Status tòa nhà: 3 là Bảo trì)
			var building = await _context.Buildings.FindAsync(apartment.BuildingId);
			bool isBuildingUnderMaintenance = building != null && (int?)building.Status == 3;

			// 2. Kiểm tra xem phòng có Hợp đồng nào đang "Có hiệu lực" không (Giả sử Status hợp đồng: 1 là Active)
			bool hasActiveContract = await _context.Contracts
				.AnyAsync(c => c.ApartmentId == apartment.ApartmentId && (int?)c.Status == 1 && c.IsDeleted == false);

			// 3. Áp dụng Logic ưu tiên
			if (isBuildingUnderMaintenance)
			{
				apartment.Status = ApartmentStatus.Maintenance; 
			}
			else if (hasActiveContract)
			{
				apartment.Status = ApartmentStatus.Occupied;     
			}
			else
			{
				apartment.Status = ApartmentStatus.Vacant;       
			}
		}

		public async Task<bool> DeleteApartmentAsync(int id, ClaimsPrincipal user = null)
		{
			var apt = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);
			if (apt == null) throw new InvalidOperationException("Không tìm thấy căn hộ.");

			if (apt.Status == (ApartmentStatus)2)
				throw new InvalidOperationException("Không thể đưa vào thùng rác vì căn hộ này đang có người ở!");

			apt.IsDeleted = true;
			apt.UpdatedAt = DateTime.UtcNow;

			var accountIdClaim = user?.FindFirst("AccountId");
			if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
				apt.UpdatedBy = parsedAccountId;

			_context.Apartments.Update(apt);
			await _context.SaveChangesAsync();
			return true;
		}

		// 2. LẤY DANH SÁCH ĐÃ XÓA
		public async Task<IEnumerable<ApartmentResponseDto>> GetDeletedApartmentsAsync()
		{
			return await _context.Apartments
				.Include(a => a.Building)
				.Where(a => a.IsDeleted == true)
				.Select(a => new ApartmentResponseDto
				{
					ApartmentId = a.ApartmentId,
					BuildingId = a.BuildingId,
					BuildingCode = a.Building != null ? a.Building.BuildingCode : null,
					ApartmentCode = a.ApartmentCode,
					ApartmentName = a.ApartmentName,
					ApartmentNumber = a.ApartmentNumber,
					FloorNumber = a.FloorNumber,
					Area = a.Area,
					Status = (byte?)a.Status
				})
				.ToListAsync();
		}

		// 3. KHÔI PHỤC
		public async Task<bool> RestoreApartmentAsync(int id)
		{
			var apt = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == true);
			if (apt == null) throw new InvalidOperationException("Không tìm thấy căn hộ trong Danh sách đã xóa.");

			apt.IsDeleted = false;
			apt.UpdatedAt = DateTime.UtcNow;

			_context.Apartments.Update(apt);
			await _context.SaveChangesAsync();
			return true;
		}

		// 4. XÓA VĨNH VIỄN
		public async Task<bool> HardDeleteApartmentAsync(int id)
		{
			var apt = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == true);
			if (apt == null) throw new InvalidOperationException("Không tìm thấy căn hộ.");

			// Chặn xóa cứng nếu căn hộ đã từng ký hợp đồng (để bảo vệ dữ liệu kế toán)
			var hasContracts = await _context.Contracts.AnyAsync(c => c.ApartmentId == id);
			if (hasContracts) throw new InvalidOperationException("Không thể xóa vĩnh viễn vì căn hộ này đã có lịch sử hợp đồng.");

			_context.Apartments.Remove(apt);
			await _context.SaveChangesAsync();
			return true;
		}
	}
}