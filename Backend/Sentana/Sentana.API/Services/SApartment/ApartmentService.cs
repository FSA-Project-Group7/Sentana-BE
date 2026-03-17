using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Apartment;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services.SApartment
{
    public class ApartmentService : IApartmentService
    {
        private readonly SentanaContext _context;

        public ApartmentService(SentanaContext context)
        {
            _context = context;
        }

		public async Task<IEnumerable<ApartmentDto>> GetApartmentListAsync()
		{
			var today = DateOnly.FromDateTime(DateTime.Now);
			return await _context.Apartments
				.Include(a => a.Contracts) 
				.Where(a => a.IsDeleted == false)
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
				.OrderByDescending(a => a.ApartmentId)
				.ToListAsync();
		}

		public async Task<ApartmentDto> CreateApartmentAsync(CreateApartmentDto dto)
        {
            var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == dto.BuildingId && b.IsDeleted == false);
            if (building == null) throw new ArgumentException("Tòa nhà này không tồn tại hoặc đã bị xóa.");
            if (building.ApartmentNumber.HasValue)
            {
                // 1. Kiểm tra sức chứa: Tòa nhà đã đầy chưa?
                int currentRoomCount = await _context.Apartments.CountAsync(a => a.BuildingId == dto.BuildingId && a.IsDeleted == false);
                if (currentRoomCount >= building.ApartmentNumber.Value)
                    throw new ArgumentException($"Tòa nhà {building.BuildingName} đã đạt số lượng tối đa ({building.ApartmentNumber.Value} phòng). Không thể tạo thêm.");

                // 2. Kiểm tra logic: Số phòng nhập vào (VD: 99) có lớn hơn tổng sức chứa của tòa nhà không?
                if (dto.ApartmentNumber > building.ApartmentNumber.Value)
                    throw new ArgumentException($"Số phòng ({dto.ApartmentNumber}) không hợp lệ vì vượt quá tổng sức chứa của tòa nhà ({building.ApartmentNumber.Value} phòng).");
            }

            // 1. Validation: Tầng không vượt quá tòa nhà
            if (dto.FloorNumber > building.FloorNumber)
                throw new ArgumentException($"Tầng của căn hộ ({dto.FloorNumber}) không được vượt quá số tầng của tòa nhà ({building.FloorNumber}).");

            // 2. Tự động tính toán Tên và Mã
            int floor = dto.FloorNumber;
            int room = dto.ApartmentNumber;

            string generatedNumberStr = $"{floor}{room:D2}"; // VD: 7 và 9 -> "709"
            int fullAppNumber = int.Parse(generatedNumberStr); // 709

            // Gắn thêm mã tòa nhà để chống trùng lặp toàn hệ thống (VD: A-709)
            string generatedCode = $"{building.BuildingCode}-{generatedNumberStr}";
            string generatedName = $"Phòng {generatedNumberStr} Chung cư SENTANA Tòa {building.BuildingName}";

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
                ApartmentCode = generatedCode,      // Tự động: A-709
                ApartmentName = generatedName,      // Tự động: Phòng 709 tòa A
                ApartmentNumber = fullAppNumber,    // Lưu số: 709
                FloorNumber = floor,                // Lưu số: 7
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
                int fullAppNumber = dto.ApartmentNumber.Value; // VD: Truyền lên 1205
                int floor = fullAppNumber / 100; // Nội suy ra Tầng 12
                int room = fullAppNumber % 100;  // Nội suy ra Phòng 5

                if (building != null)
                {
                    
                    if (building.FloorNumber.HasValue && floor > building.FloorNumber.Value)
                        throw new ArgumentException($"Tầng phân tích được ({floor}) vượt quá số tầng của tòa nhà ({building.FloorNumber.Value}).");

                    
                    if (building.ApartmentNumber.HasValue && room > building.ApartmentNumber.Value)
                        throw new ArgumentException($"Số phòng phân tích được ({room}) vượt quá tổng số phòng của tòa nhà ({building.ApartmentNumber.Value} phòng).");
                }

                if (room <= 0 || room >= 100)
                    throw new ArgumentException("Số phòng phân tích được không hợp lệ (phải từ 01 đến 99).");

                string generatedNumberStr = $"{floor}{room:D2}";
                string generatedCode = $"{building?.BuildingCode}-{generatedNumberStr}";
                string generatedName = $"Phòng {generatedNumberStr} Chung cư SENTANA Tòa {building?.BuildingName}";

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

            apartment.Status = (ApartmentStatus)newStatus;
            apartment.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
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