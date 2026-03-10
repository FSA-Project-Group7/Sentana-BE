using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Utility;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class UtilityService : IUtilityService
    {
        private readonly SentanaContext _context;

        public UtilityService(SentanaContext context)
        {
            _context = context;
        }

        // hàm kiểm tra phòng có tồn tại và đang có người ở không
        private async Task<(bool IsValid, string ErrorMessage)> CheckApartmentValidAsync(int apartmentId)
        {
            var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == apartmentId && a.IsDeleted == false);
            if (apartment == null) return (false, "Không tìm thấy căn hộ.");
            if (apartment.Status != Enums.ApartmentStatus.Occupied) return (false, "Căn hộ hiện không có người ở, không thể ghi số Điện/Nước.");
            return (true, string.Empty);
        }

        // nhập chỉ số điện 
        public async Task<(bool IsSuccess, string Message)> InputElectricityIndexAsync(InputElectricIndexDto request, int currentUserId)
        {
            // kiểm tra phòng có tồn tại và đang có người ở không
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            // kiểm tra xem tháng này đã chốt số chưa ( tránh nhập trùng Data )
            var existingRecord = await _context.ElectricMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Căn hộ này đã được chốt số điện cho tháng {request.RegistrationDate.Month}/{request.RegistrationDate.Year}.");

            // tự động tìm OldIndex từ tháng gần nhất
            var lastRecord = await _context.ElectricMeters
                .Where(e => e.ApartmentId == request.ApartmentId && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate)
                .FirstOrDefaultAsync();
            // nếu phòng này mới tinh chưa nhập bao giờ, số cũ = 0. Nếu đã từng nhập, lấy số mới của tháng trước làm số cũ cho tháng này.
            decimal oldIndex = lastRecord != null && lastRecord.NewIndex.HasValue ? lastRecord.NewIndex.Value : 0m;

            // chỉ số mới bắt buộc phải lớn hơn hoặc bằng chỉ số cũ
            if (request.NewIndex < oldIndex)
                return (false, $"Chỉ số mới ({request.NewIndex}) không được nhỏ hơn chỉ số cũ ({oldIndex}).");

            // lấy đơn giá
            var electricService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceName.Contains("Điện") && s.IsDeleted == false);
            decimal pricePerKwh = electricService?.ServiceFee ?? 3500m; // Fallback về 3500 nếu chưa có trong DB


            // lưu
            var newElectricMeter = new ElectricMeter
            {
                ApartmentId = request.ApartmentId,
                RegistrationDate = request.RegistrationDate,
                OldIndex = oldIndex,
                NewIndex = request.NewIndex,
                Price = pricePerKwh,
                Status = GeneralStatus.Active,
                CreatedBy = currentUserId, // Lưu lại ID của Admin đã thao tác
                CreatedAt = DateTime.Now
            };

            _context.ElectricMeters.Add(newElectricMeter);
            bool isSaved = await _context.SaveChangesAsync() > 0;

            return isSaved ? (true, "Ghi nhận chỉ số điện thành công!") : (false, "Lỗi khi lưu dữ liệu vào hệ thống.");
        }


        // nhập chỉ số nước
        public async Task<(bool IsSuccess, string Message)> InputWaterIndexAsync(InputWaterIndexDto request, int currentUserId)
        {
            // yìm căn hộ trong db
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            // kiểm tra xem tháng này đã chốt số chưa ( tránh nhập trùng Data )
            var existingRecord = await _context.WaterMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Đã chốt số nước cho tháng {request.RegistrationDate.Month}/{request.RegistrationDate.Year}.");

            // tự động tìm OldIndex từ tháng gần nhất
            var lastRecord = await _context.WaterMeters
                .Where(e => e.ApartmentId == request.ApartmentId && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate)
                .FirstOrDefaultAsync();

            // nếu phòng này mới tinh chưa nhập bao giờ, số cũ = 0. Nếu đã từng nhập, lấy số mới của tháng trước làm số cũ cho tháng này.
            decimal oldIndex = lastRecord != null && lastRecord.NewIndex.HasValue ? lastRecord.NewIndex.Value : 0m;

            // số nhập vào phải >= số cũ.
            if (request.NewIndex < oldIndex)
                return (false, $"Chỉ số mới ({request.NewIndex}) không được nhỏ hơn chỉ số cũ ({oldIndex}).");

            // lấy đơn giá
            var waterService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceName.Contains("Nước") && s.IsDeleted == false);
            decimal pricePerM3 = waterService?.ServiceFee ?? 25000m; // Fallback về 25000 nếu chưa có trong DB

            var newWaterMeter = new WaterMeter
            {
                ApartmentId = request.ApartmentId,
                RegistrationDate = request.RegistrationDate,
                OldIndex = oldIndex,
                NewIndex = request.NewIndex,
                Price = pricePerM3,
                Status = Enums.GeneralStatus.Active,
                CreatedBy = currentUserId, // Lưu lại ID của Admin đã thao tác
                CreatedAt = DateTime.Now
            };

            _context.WaterMeters.Add(newWaterMeter);
            bool isSaved = await _context.SaveChangesAsync() > 0;

            return isSaved ? (true, "Ghi nhận chỉ số nước thành công!") : (false, "Lỗi khi lưu dữ liệu nước.");
        }
    }
}