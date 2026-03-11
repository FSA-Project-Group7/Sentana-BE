using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Utility;
using Sentana.API.Enums;
using Sentana.API.Models;
using System.Security.Claims;
using Sentana.API.Helpers; 

namespace Sentana.API.Services
{
    public class UtilityService : IUtilityService
    {
        private readonly SentanaContext _context;

        public UtilityService(SentanaContext context)
        {
            _context = context;
        }

        // Hàm kiểm tra phòng
        private async Task<(bool IsValid, string ErrorMessage)> CheckApartmentValidAsync(int apartmentId)
        {
            var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == apartmentId && a.IsDeleted == false);
            if (apartment == null) return (false, "Không tìm thấy căn hộ.");
            if (apartment.Status != Enums.ApartmentStatus.Occupied) return (false, "Căn hộ hiện không có người ở, không thể ghi số Điện/Nước.");
            return (true, string.Empty);
        }

        // Input chỉ số điện
        public async Task<(bool IsSuccess, string Message)> InputElectricityIndexAsync(InputElectricIndexDto request, int currentUserId)
        {
            // Kiểm tra phòng
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            // Chống nhập trùng Data trong cùng tháng
            var existingRecord = await _context.ElectricMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Căn hộ này đã được chốt số điện cho tháng {request.RegistrationDate.Month}/{request.RegistrationDate.Year}.");

            // Tự động tìm OldIndex từ tháng gần nhất
            var lastRecord = await _context.ElectricMeters
                .Where(e => e.ApartmentId == request.ApartmentId && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate)
                .FirstOrDefaultAsync();

            decimal oldIndex = lastRecord != null && lastRecord.NewIndex.HasValue ? lastRecord.NewIndex.Value : 0m;

            var validationResult = ValidationHelper.ValidateUtilityIndex(request.NewIndex, oldIndex, request.RegistrationDate);
            if (!validationResult.IsValid)
            {
                return (false, validationResult.ErrorMessage);
            }

            // Lấy đơn giá động
            var electricService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceName.Contains("Điện") && s.IsDeleted == false);
            decimal pricePerKwh = electricService?.ServiceFee ?? 3500m;

            // Lưu Data
            var newElectricMeter = new ElectricMeter
            {
                ApartmentId = request.ApartmentId,
                RegistrationDate = request.RegistrationDate,
                OldIndex = oldIndex,
                NewIndex = request.NewIndex,
                Price = pricePerKwh,
                Status = GeneralStatus.Active,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.Now
            };

            _context.ElectricMeters.Add(newElectricMeter);
            bool isSaved = await _context.SaveChangesAsync() > 0;

            return isSaved ? (true, "Ghi nhận chỉ số điện thành công!") : (false, "Lỗi khi lưu dữ liệu vào hệ thống.");
        }

        // Input chỉ số nước
        public async Task<(bool IsSuccess, string Message)> InputWaterIndexAsync(InputWaterIndexDto request, int currentUserId)
        {
            // Kiểm tra phòng
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            // Chống nhập trùng Data trong cùng tháng
            var existingRecord = await _context.WaterMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Đã chốt số nước cho tháng {request.RegistrationDate.Month}/{request.RegistrationDate.Year}.");

            // Tự động tìm OldIndex từ tháng gần nhất
            var lastRecord = await _context.WaterMeters
                .Where(e => e.ApartmentId == request.ApartmentId && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate)
                .FirstOrDefaultAsync();

            decimal oldIndex = lastRecord != null && lastRecord.NewIndex.HasValue ? lastRecord.NewIndex.Value : 0m;

            var validationResult = ValidationHelper.ValidateUtilityIndex(request.NewIndex, oldIndex, request.RegistrationDate);
            if (!validationResult.IsValid)
            {
                return (false, validationResult.ErrorMessage);
            }

            // Lấy đơn giá động
            var waterService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceName.Contains("Nước") && s.IsDeleted == false);
            decimal pricePerM3 = waterService?.ServiceFee ?? 25000m;

            // Lưu Data
            var newWaterMeter = new WaterMeter
            {
                ApartmentId = request.ApartmentId,
                RegistrationDate = request.RegistrationDate,
                OldIndex = oldIndex,
                NewIndex = request.NewIndex,
                Price = pricePerM3,
                Status = Enums.GeneralStatus.Active,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.Now
            };

            _context.WaterMeters.Add(newWaterMeter);
            bool isSaved = await _context.SaveChangesAsync() > 0;

            return isSaved ? (true, "Ghi nhận chỉ số nước thành công!") : (false, "Lỗi khi lưu dữ liệu nước.");
        }

        // Utility history
        public async Task<(bool IsSuccess, string Message, List<UtilityHistoryDto>? Data)> GetUtilityHistoryAsync(ClaimsPrincipal user, int? targetApartmentId, int? month, int? year)
        {
            var valResult = ValidationHelper.ValidateMonthYear(month, year);
            if (!valResult.IsValid) return (false, valResult.ErrorMessage, null);

            // lấy thông tin người dùng
            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                return (false, "Token không hợp lệ.", null);

            var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

            int resolvedApartmentId = 0;

            if (isManager)
            {
                if (!targetApartmentId.HasValue) return (false, "Vui lòng cung cấp ID căn hộ.", null);
                resolvedApartmentId = targetApartmentId.Value;
            }
            else // Nếu là Resident
            {
                var contract = await _context.Contracts
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (contract == null || !contract.ApartmentId.HasValue)
                    return (false, "Không tìm thấy hợp đồng thuê nhà đang hiệu lực của bạn.", null);

                resolvedApartmentId = contract.ApartmentId.Value; // Tự động lấy phòng của Cư dân
            }

            var elecQuery = _context.ElectricMeters.Where(e => e.ApartmentId == resolvedApartmentId && e.IsDeleted == false);
            if (month.HasValue) elecQuery = elecQuery.Where(e => e.RegistrationDate.HasValue && e.RegistrationDate.Value.Month == month.Value);
            if (year.HasValue) elecQuery = elecQuery.Where(e => e.RegistrationDate.HasValue && e.RegistrationDate.Value.Year == year.Value);
            var elecList = await elecQuery.ToListAsync();

            var waterQuery = _context.WaterMeters.Where(w => w.ApartmentId == resolvedApartmentId && w.IsDeleted == false);
            if (month.HasValue) waterQuery = waterQuery.Where(w => w.RegistrationDate.HasValue && w.RegistrationDate.Value.Month == month.Value);
            if (year.HasValue) waterQuery = waterQuery.Where(w => w.RegistrationDate.HasValue && w.RegistrationDate.Value.Year == year.Value);
            var waterList = await waterQuery.ToListAsync();

            var history = new List<UtilityHistoryDto>();

            var dates = elecList.Select(e => new { e.RegistrationDate!.Value.Month, e.RegistrationDate!.Value.Year })
                .Union(waterList.Select(w => new { w.RegistrationDate!.Value.Month, w.RegistrationDate!.Value.Year }))
                .Distinct()
                .OrderByDescending(d => d.Year).ThenByDescending(d => d.Month);

            foreach (var date in dates)
            {
                var elec = elecList.FirstOrDefault(e => e.RegistrationDate!.Value.Month == date.Month && e.RegistrationDate!.Value.Year == date.Year);
                var water = waterList.FirstOrDefault(w => w.RegistrationDate!.Value.Month == date.Month && w.RegistrationDate!.Value.Year == date.Year);

                history.Add(new UtilityHistoryDto
                {
                    Month = date.Month,
                    Year = date.Year,
                    ElectricityOldIndex = elec?.OldIndex ?? 0,
                    ElectricityNewIndex = elec?.NewIndex ?? 0,
                    WaterOldIndex = water?.OldIndex ?? 0,
                    WaterNewIndex = water?.NewIndex ?? 0
                });
            }

            return (true, "Thành công", history);
        }
    }
}