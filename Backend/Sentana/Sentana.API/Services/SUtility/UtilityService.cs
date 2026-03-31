using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Utility;
using Sentana.API.Enums;
using Sentana.API.Models;
using System.Security.Claims;
using Sentana.API.Helpers;
using OfficeOpenXml;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sentana.API.Services
{
    public class UtilityService : IUtilityService
    {
        private readonly SentanaContext _context;

        public UtilityService(SentanaContext context)
        {
            _context = context;
        }

        // Hàm kiểm tra phòng và lấy ngày bắt đầu Hợp đồng
        private async Task<(bool IsValid, string ErrorMessage, DateTime? ContractStartDate)> CheckApartmentValidAsync(int apartmentId)
        {
            var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == apartmentId && a.IsDeleted == false);
            if (apartment == null) return (false, "Hệ thống không tìm thấy thông tin căn hộ này.", null);
            if (apartment.Status != Enums.ApartmentStatus.Occupied) return (false, "Căn hộ này hiện đang trống, chưa có người thuê nên không thể ghi chỉ số.", null);

            var activeContract = await _context.Contracts
                .Where(c => c.ApartmentId == apartmentId && c.Status == Enums.GeneralStatus.Active && c.IsDeleted == false)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (activeContract == null || !activeContract.StartDay.HasValue)
                return (false, "Căn hộ này hiện chưa có hợp đồng thuê nhà hợp lệ trên hệ thống.", null);

            DateTime contractStartDate = activeContract.StartDay.Value.ToDateTime(TimeOnly.MinValue);

            return (true, string.Empty, contractStartDate);
        }

        // Nhập chỉ số điện
        public async Task<(bool IsSuccess, string Message)> InputElectricityIndexAsync(InputElectricIndexDto request, int currentUserId)
        {
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            int requestTotalMonths = request.RegistrationDate.Year * 12 + request.RegistrationDate.Month;
            int contractStartTotalMonths = aptCheck.ContractStartDate!.Value.Year * 12 + aptCheck.ContractStartDate.Value.Month;

            // Bắt lỗi nhập trước ngày thuê
            if (requestTotalMonths < contractStartTotalMonths)
            {
                return (false, $"Khách hàng mới dọn vào từ tháng {aptCheck.ContractStartDate.Value.Month}/{aptCheck.ContractStartDate.Value.Year}. Bạn chỉ có thể ghi chỉ số bắt đầu từ mốc thời gian này trở đi.");
            }

            // Bắt lỗi nhập trùng
            var existingRecord = await _context.ElectricMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Tháng {request.RegistrationDate.Month}/{request.RegistrationDate.Year} đã được chốt chỉ số điện rồi, bạn không cần nhập lại nữa.");

            // Lấy tháng gần nhất đã nhập để kiểm tra tính tuần tự
            var previousRecord = await _context.ElectricMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) < requestTotalMonths
                         && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            if (requestTotalMonths > contractStartTotalMonths)
            {
                if (previousRecord == null)
                    return (false, $"Bạn chưa chốt số điện của tháng đầu tiên khách dọn vào. Vui lòng ghi sổ theo thứ tự từ tháng {aptCheck.ContractStartDate.Value.Month}/{aptCheck.ContractStartDate.Value.Year}.");

                int previousTotalMonths = previousRecord.RegistrationDate!.Value.Year * 12 + previousRecord.RegistrationDate.Value.Month;
                if (requestTotalMonths - previousTotalMonths > 1)
                    return (false, $"Tháng gần nhất bạn ghi sổ là tháng {previousRecord.RegistrationDate.Value.Month}/{previousRecord.RegistrationDate.Value.Year}. Vui lòng nhập cho tháng kế tiếp, không được bỏ trống tháng ở giữa.");
            }

            decimal oldIndex = previousRecord?.NewIndex ?? 0m;

            // Kiểm tra tính hợp lý với dữ liệu tương lai
            var nextRecord = await _context.ElectricMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) > requestTotalMonths
                         && e.IsDeleted == false)
                .OrderBy(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            if (nextRecord != null && nextRecord.NewIndex.HasValue && request.NewIndex > nextRecord.NewIndex.Value)
                return (false, $"Vô lý quá, chỉ số bạn đang nhập ({request.NewIndex}) lại lớn hơn cả tháng tương lai ({nextRecord.RegistrationDate!.Value.Month}/{nextRecord.RegistrationDate.Value.Year} đang là {nextRecord.NewIndex.Value}). Hãy kiểm tra lại số liệu.");

            var validationResult = ValidationHelper.ValidateUtilityIndex(request.NewIndex, oldIndex, request.RegistrationDate);
            if (!validationResult.IsValid) return (false, validationResult.ErrorMessage);

            var electricService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceName.Contains("Điện") && s.IsDeleted == false);
            decimal pricePerKwh = electricService?.ServiceFee ?? 3500m;

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

            return isSaved ? (true, "Đã lưu chỉ số điện thành công!") : (false, "Có lỗi xảy ra khi lưu vào cơ sở dữ liệu.");
        }

        // Nhập chỉ số nước
        public async Task<(bool IsSuccess, string Message)> InputWaterIndexAsync(InputWaterIndexDto request, int currentUserId)
        {
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            int requestTotalMonths = request.RegistrationDate.Year * 12 + request.RegistrationDate.Month;
            int contractStartTotalMonths = aptCheck.ContractStartDate!.Value.Year * 12 + aptCheck.ContractStartDate.Value.Month;

            if (requestTotalMonths < contractStartTotalMonths)
                return (false, $"Khách hàng mới dọn vào từ tháng {aptCheck.ContractStartDate.Value.Month}/{aptCheck.ContractStartDate.Value.Year}. Bạn chỉ có thể ghi chỉ số bắt đầu từ mốc thời gian này trở đi.");

            var existingRecord = await _context.WaterMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Tháng {request.RegistrationDate.Month}/{request.RegistrationDate.Year} đã được chốt số nước rồi, bạn không cần nhập lại nữa.");

            var previousRecord = await _context.WaterMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) < requestTotalMonths
                         && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            if (requestTotalMonths > contractStartTotalMonths)
            {
                if (previousRecord == null)
                    return (false, $"Bạn chưa chốt số nước của tháng đầu tiên khách dọn vào. Vui lòng ghi sổ theo thứ tự từ tháng {aptCheck.ContractStartDate.Value.Month}/{aptCheck.ContractStartDate.Value.Year} trước nhé.");

                int previousTotalMonths = previousRecord.RegistrationDate!.Value.Year * 12 + previousRecord.RegistrationDate.Value.Month;
                if (requestTotalMonths - previousTotalMonths > 1)
                    return (false, $"Tháng gần nhất bạn ghi sổ là tháng {previousRecord.RegistrationDate.Value.Month}/{previousRecord.RegistrationDate.Value.Year}. Vui lòng nhập cho tháng kế tiếp, không được bỏ trống tháng ở giữa.");
            }

            decimal oldIndex = previousRecord?.NewIndex ?? 0m;

            var nextRecord = await _context.WaterMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) > requestTotalMonths
                         && e.IsDeleted == false)
                .OrderBy(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            if (nextRecord != null && nextRecord.NewIndex.HasValue && request.NewIndex > nextRecord.NewIndex.Value)
                return (false, $"Vô lý quá, chỉ số bạn đang nhập ({request.NewIndex}) lại lớn hơn cả tháng tương lai ({nextRecord.RegistrationDate!.Value.Month}/{nextRecord.RegistrationDate.Value.Year} đang là {nextRecord.NewIndex.Value}). Hãy kiểm tra lại số liệu.");

            var validationResult = ValidationHelper.ValidateUtilityIndex(request.NewIndex, oldIndex, request.RegistrationDate);
            if (!validationResult.IsValid) return (false, validationResult.ErrorMessage);

            var waterService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceName.Contains("Nước") && s.IsDeleted == false);
            decimal pricePerM3 = waterService?.ServiceFee ?? 25000m;

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

            return isSaved ? (true, "Đã lưu chỉ số nước thành công!") : (false, "Có lỗi xảy ra khi lưu vào cơ sở dữ liệu.");
        }

        // Lịch sử điện nước
        public async Task<(bool IsSuccess, string Message, List<UtilityHistoryDto>? Data)> GetUtilityHistoryAsync(ClaimsPrincipal user, int? targetApartmentId, int? month, int? year)
        {
            var valResult = ValidationHelper.ValidateMonthYear(month, year);
            if (!valResult.IsValid) return (false, valResult.ErrorMessage, null);

            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                return (false, "Phiên đăng nhập không hợp lệ.", null);

            var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

            int resolvedApartmentId = 0;

            if (isManager)
            {
                if (!targetApartmentId.HasValue) return (false, "Vui lòng chọn một căn hộ để xem.", null);
                resolvedApartmentId = targetApartmentId.Value;
            }
            else
            {
                var contract = await _context.Contracts
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (contract == null || !contract.ApartmentId.HasValue)
                    return (false, "Bạn hiện chưa có hợp đồng thuê nhà nào đang hoạt động.", null);

                resolvedApartmentId = contract.ApartmentId.Value;
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

            return (true, "Lấy dữ liệu thành công", history);
        }

        // Import Excel
        public async Task<(bool IsSuccess, string Message)> ImportUtilityExcelAsync(IFormFile file, string utilityType, int currentUserId)
        {
            if (file == null || file.Length == 0) return (false, "Bạn chưa chọn file Excel nào.");

            ExcelPackage.License.SetNonCommercialPersonal("Sentana Project");

            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null || worksheet.Dimension == null)
                return (false, "File Excel đang trống hoặc bị sai định dạng chuẩn.");

            int rowCount = worksheet.Dimension.Rows;
            int successCount = 0;

            for (int row = 2; row <= rowCount; row++)
            {
                if (int.TryParse(worksheet.Cells[row, 1].Text, out int aptId) &&
                    decimal.TryParse(worksheet.Cells[row, 2].Text, out decimal newIndex) &&
                    DateTime.TryParse(worksheet.Cells[row, 3].Text, out DateTime regDate))
                {
                    if (utilityType.ToLower() == "electric")
                    {
                        var dto = new InputElectricIndexDto { ApartmentId = aptId, NewIndex = newIndex, RegistrationDate = regDate };
                        var res = await InputElectricityIndexAsync(dto, currentUserId);
                        if (res.IsSuccess) successCount++;
                    }
                    else if (utilityType.ToLower() == "water")
                    {
                        var dto = new InputWaterIndexDto { ApartmentId = aptId, NewIndex = newIndex, RegistrationDate = regDate };
                        var res = await InputWaterIndexAsync(dto, currentUserId);
                        if (res.IsSuccess) successCount++;
                    }
                }
            }

            if (successCount == 0)
                return (false, "Không thể import được dòng nào. Vui lòng kiểm tra lại file xem có bị nhảy cóc tháng hoặc sai thông tin phòng không.");

            return (true, $"Đã cập nhật thành công {successCount} dòng dữ liệu từ file Excel.");
        }
    }
}