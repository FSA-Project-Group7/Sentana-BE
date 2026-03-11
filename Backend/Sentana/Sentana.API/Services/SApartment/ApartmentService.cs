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
            return await _context.Apartments
                .Where(a => a.IsDeleted == false)
                .Select(a => new ApartmentDto
                {
                    ApartmentId = a.ApartmentId,
                    ApartmentCode = a.ApartmentCode,
                    ApartmentName = a.ApartmentName,
                    FloorNumber = a.FloorNumber,
                    Area = a.Area,
                    Status = (byte?)a.Status
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
            string generatedName = $"Phòng {generatedNumberStr} tòa {building.BuildingName}";

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
                    // Validation: Tầng nội suy ra không được vượt số tầng tòa nhà
                    if (building.FloorNumber.HasValue && floor > building.FloorNumber.Value)
                        throw new ArgumentException($"Tầng phân tích được ({floor}) vượt quá số tầng của tòa nhà ({building.FloorNumber.Value}).");

                    // --- [MỚI] CHỐT CHẶN: Kiểm tra logic số phòng khi Update ---
                    if (building.ApartmentNumber.HasValue && room > building.ApartmentNumber.Value)
                        throw new ArgumentException($"Số phòng phân tích được ({room}) vượt quá tổng số phòng của tòa nhà ({building.ApartmentNumber.Value} phòng).");
                }

                if (room <= 0 || room >= 100)
                    throw new ArgumentException("Số phòng phân tích được không hợp lệ (phải từ 01 đến 99).");

                string generatedNumberStr = $"{floor}{room:D2}";
                string generatedCode = $"{building?.BuildingCode}-{generatedNumberStr}";
                string generatedName = $"Phòng {generatedNumberStr} tòa {building?.BuildingName}";

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

        public async Task<bool> DeleteApartmentAsync(int id)
        {
            var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);
            if (apartment == null) return false;

            // CHỐT CHẶN BẢO VỆ DỮ LIỆU: Chỉ xóa khi phòng trống
            if (apartment.Status != ApartmentStatus.Vacant)
                throw new ArgumentException("Không thể xóa căn hộ đang hoạt động hoặc bảo trì. Vui lòng chuyển trạng thái về 'Trống' trước khi xóa.");

            apartment.IsDeleted = true;
            apartment.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}