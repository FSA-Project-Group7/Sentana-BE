using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Utility;
using Sentana.API.Enums;
using Sentana.API.Models;
using System.Security.Claims;
using Sentana.API.Helpers;
using OfficeOpenXml;
using System.Linq;
using System.Collections.Generic;
using System;
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

        private async Task<(bool IsValid, string ErrorMessage, DateTime? ContractStartDate)> CheckApartmentValidAsync(int apartmentId)
        {
            var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == apartmentId && a.IsDeleted == false);
            if (apartment == null) return (false, "Hệ thống không tìm thấy thông tin căn hộ.", null);
            if (apartment.Status != Enums.ApartmentStatus.Occupied) return (false, "Căn hộ không ở trạng thái đang sử dụng. Không thể ghi nhận chỉ số.", null);

            var activeContract = await _context.Contracts
                .Where(c => c.ApartmentId == apartmentId && c.Status == Enums.GeneralStatus.Active && c.IsDeleted == false)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (activeContract == null || !activeContract.StartDay.HasValue)
                return (false, "Căn hộ chưa có hợp đồng thuê hiệu lực trên hệ thống.", null);

            DateTime contractStartDate = activeContract.StartDay.Value.ToDateTime(TimeOnly.MinValue);

            return (true, string.Empty, contractStartDate);
        }

        public async Task<(bool IsSuccess, string Message)> InputElectricityIndexAsync(InputElectricIndexDto request, int currentUserId)
        {
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            int requestMonthId = request.RegistrationDate.Year * 12 + request.RegistrationDate.Month;
            int contractStartMonthId = aptCheck.ContractStartDate!.Value.Year * 12 + aptCheck.ContractStartDate.Value.Month;

            if (requestMonthId < contractStartMonthId)
            {
                return (false, $"Hợp đồng bắt đầu từ tháng {aptCheck.ContractStartDate.Value.Month}/{aptCheck.ContractStartDate.Value.Year}. Không thể ghi nhận chỉ số cho kỳ trước mốc thời gian này.");
            }

            var existingRecord = await _context.ElectricMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Chỉ số điện kỳ {request.RegistrationDate.Month}/{request.RegistrationDate.Year} đã tồn tại.");

            var previousRecord = await _context.ElectricMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) < requestMonthId
                         && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            var nextRecord = await _context.ElectricMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) > requestMonthId
                         && e.IsDeleted == false)
                .OrderBy(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            int prevMonthId = previousRecord != null ? previousRecord.RegistrationDate!.Value.Year * 12 + previousRecord.RegistrationDate.Value.Month : contractStartMonthId - 1;

            if (nextRecord == null)
            {
                if (requestMonthId - prevMonthId > 1 && !request.IsMerge)
                {
                    return (false, $"REQUIRE_MERGE|Hệ thống phát hiện thiếu hụt dữ liệu kỳ trước. Bạn có muốn gộp lũy kế vào kỳ {request.RegistrationDate.Month}/{request.RegistrationDate.Year} không?");
                }
            }

            decimal oldIndex = previousRecord?.NewIndex ?? 0m;

            if (request.NewIndex < oldIndex)
                return (false, "Chỉ số mới không hợp lệ. Khối lượng tiêu thụ không được nhỏ hơn kỳ trước.");

            if (nextRecord != null && request.NewIndex > nextRecord.NewIndex.Value)
                return (false, $"Chỉ số mới không hợp lệ. Giá trị vượt quá chỉ số đã chốt của kỳ tương lai ({nextRecord.RegistrationDate!.Value.Month}/{nextRecord.RegistrationDate.Value.Year}: {nextRecord.NewIndex.Value}).");

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

            if (nextRecord != null)
            {
                nextRecord.OldIndex = request.NewIndex;
                _context.ElectricMeters.Update(nextRecord);
            }

            bool isSaved = await _context.SaveChangesAsync() > 0;
            return isSaved ? (true, "Ghi nhận dữ liệu thành công.") : (false, "Lỗi truy xuất cơ sở dữ liệu.");
        }

        public async Task<(bool IsSuccess, string Message)> InputWaterIndexAsync(InputWaterIndexDto request, int currentUserId)
        {
            var aptCheck = await CheckApartmentValidAsync(request.ApartmentId);
            if (!aptCheck.IsValid) return (false, aptCheck.ErrorMessage);

            int requestMonthId = request.RegistrationDate.Year * 12 + request.RegistrationDate.Month;
            int contractStartMonthId = aptCheck.ContractStartDate!.Value.Year * 12 + aptCheck.ContractStartDate.Value.Month;

            if (requestMonthId < contractStartMonthId)
                return (false, $"Hợp đồng bắt đầu từ tháng {aptCheck.ContractStartDate.Value.Month}/{aptCheck.ContractStartDate.Value.Year}. Không thể ghi nhận chỉ số cho kỳ trước mốc thời gian này.");

            var existingRecord = await _context.WaterMeters
                .FirstOrDefaultAsync(e => e.ApartmentId == request.ApartmentId
                                       && e.RegistrationDate.HasValue
                                       && e.RegistrationDate.Value.Month == request.RegistrationDate.Month
                                       && e.RegistrationDate.Value.Year == request.RegistrationDate.Year
                                       && e.IsDeleted == false);
            if (existingRecord != null)
                return (false, $"Chỉ số nước kỳ {request.RegistrationDate.Month}/{request.RegistrationDate.Year} đã tồn tại.");

            var previousRecord = await _context.WaterMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) < requestMonthId
                         && e.IsDeleted == false)
                .OrderByDescending(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            var nextRecord = await _context.WaterMeters
                .Where(e => e.ApartmentId == request.ApartmentId
                         && e.RegistrationDate.HasValue
                         && (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) > requestMonthId
                         && e.IsDeleted == false)
                .OrderBy(e => e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month)
                .FirstOrDefaultAsync();

            int prevMonthId = previousRecord != null ? previousRecord.RegistrationDate!.Value.Year * 12 + previousRecord.RegistrationDate.Value.Month : contractStartMonthId - 1;

            if (nextRecord == null)
            {
                if (requestMonthId - prevMonthId > 1 && !request.IsMerge)
                {
                    return (false, $"REQUIRE_MERGE|Hệ thống phát hiện thiếu hụt dữ liệu kỳ trước. Bạn có muốn gộp lũy kế vào kỳ {request.RegistrationDate.Month}/{request.RegistrationDate.Year} không?");
                }
            }

            decimal oldIndex = previousRecord?.NewIndex ?? 0m;

            if (request.NewIndex < oldIndex)
                return (false, "Chỉ số mới không hợp lệ. Khối lượng tiêu thụ không được nhỏ hơn kỳ trước.");

            if (nextRecord != null && request.NewIndex > nextRecord.NewIndex.Value)
                return (false, $"Chỉ số mới không hợp lệ. Giá trị vượt quá chỉ số đã chốt của kỳ tương lai ({nextRecord.RegistrationDate!.Value.Month}/{nextRecord.RegistrationDate.Value.Year}: {nextRecord.NewIndex.Value}).");

            var waterService = await _context.Services.FirstOrDefaultAsync(s => s.ServiceName.Contains("Nước") && s.IsDeleted == false);
            decimal pricePerM3 = waterService?.ServiceFee ?? 25000m;

            var newWaterMeter = new WaterMeter
            {
                ApartmentId = request.ApartmentId,
                RegistrationDate = request.RegistrationDate,
                OldIndex = oldIndex,
                NewIndex = request.NewIndex,
                Price = pricePerM3,
                Status = GeneralStatus.Active,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.Now
            };

            _context.WaterMeters.Add(newWaterMeter);

            if (nextRecord != null)
            {
                nextRecord.OldIndex = request.NewIndex;
                _context.WaterMeters.Update(nextRecord);
            }

            bool isSaved = await _context.SaveChangesAsync() > 0;
            return isSaved ? (true, "Ghi nhận dữ liệu thành công.") : (false, "Lỗi truy xuất cơ sở dữ liệu.");
        }

        public async Task<(bool IsSuccess, string Message, List<UtilityHistoryDto>? Data)> GetUtilityHistoryAsync(ClaimsPrincipal user, int? targetApartmentId, int? month, int? year)
        {
            var valResult = ValidationHelper.ValidateMonthYear(month, year);
            if (!valResult.IsValid) return (false, valResult.ErrorMessage, null);

            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                return (false, "Xác thực danh tính thất bại.", null);

            var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

            var targetContracts = new List<Contract>();

            if (isManager)
            {
                if (!targetApartmentId.HasValue) return (false, "Dữ liệu đầu vào không hợp lệ. Yêu cầu cung cấp định danh căn hộ.", null);
                var contract = await _context.Contracts
                    .Include(c => c.Apartment)
                    .Where(c => c.ApartmentId == targetApartmentId.Value && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
                if (contract != null) targetContracts.Add(contract);
            }
            else
            {
                targetContracts = await _context.Contracts
                    .Include(c => c.Apartment)
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .ToListAsync();
            }

            if (!targetContracts.Any())
                return (false, "Hệ thống không tìm thấy hợp đồng hiệu lực.", null);

            var targetApartmentIds = targetContracts.Select(c => c.ApartmentId.Value).ToList();

            var elecList = await _context.ElectricMeters
                .Where(e => e.ApartmentId.HasValue && targetApartmentIds.Contains(e.ApartmentId.Value) && e.IsDeleted == false)
                .ToListAsync();

            var waterList = await _context.WaterMeters
                .Where(w => w.ApartmentId.HasValue && targetApartmentIds.Contains(w.ApartmentId.Value) && w.IsDeleted == false)
                .ToListAsync();

            var invoices = await _context.Invoices
                .Where(i => i.ApartmentId.HasValue && targetApartmentIds.Contains(i.ApartmentId.Value) && i.IsDeleted == false)
                .Select(i => new { i.ApartmentId, i.BillingMonth, i.BillingYear })
                .ToListAsync();

            var history = new List<UtilityHistoryDto>();

            var datesAndApts = elecList.Select(e => new { AptId = e.ApartmentId, AptCode = e.Apartment?.ApartmentCode, Month = e.RegistrationDate!.Value.Month, Year = e.RegistrationDate!.Value.Year })
                .Union(waterList.Select(w => new { AptId = w.ApartmentId, AptCode = w.Apartment?.ApartmentCode, Month = w.RegistrationDate!.Value.Month, Year = w.RegistrationDate!.Value.Year }))
                .Distinct()
                .ToList();

            foreach (var contract in targetContracts)
            {
                var aptData = datesAndApts.Where(d => d.AptId == contract.ApartmentId).ToList();
                DateTime startDate = contract.StartDay?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue;

                // ĐÃ SỬA: LUÔN LUÔN đảm bảo tháng đầu tiên của Hợp đồng có mặt trong danh sách (dù DB có dữ liệu hay không)
                if (contract.StartDay.HasValue)
                {
                    if (!aptData.Any(d => d.Month == startDate.Month && d.Year == startDate.Year))
                    {
                        aptData.Add(new { AptId = contract.ApartmentId, AptCode = contract.Apartment?.ApartmentCode, Month = startDate.Month, Year = startDate.Year });
                    }
                }

                foreach (var d in aptData)
                {
                    if (month.HasValue && month.Value != d.Month) continue;
                    if (year.HasValue && year.Value != d.Year) continue;

                    var elec = elecList.FirstOrDefault(e => e.ApartmentId == d.AptId && e.RegistrationDate!.Value.Month == d.Month && e.RegistrationDate!.Value.Year == d.Year);
                    var water = waterList.FirstOrDefault(w => w.ApartmentId == d.AptId && w.RegistrationDate!.Value.Month == d.Month && w.RegistrationDate!.Value.Year == d.Year);
                    bool hasInvoice = invoices.Any(i => i.ApartmentId == d.AptId && i.BillingMonth == d.Month && i.BillingYear == d.Year);

                    history.Add(new UtilityHistoryDto
                    {
                        ApartmentId = d.AptId ?? 0,
                        ApartmentCode = d.AptCode ?? "N/A",
                        Month = d.Month,
                        Year = d.Year,
                        ElectricityOldIndex = elec?.OldIndex ?? 0,
                        ElectricityNewIndex = elec?.NewIndex ?? 0,
                        WaterOldIndex = water?.OldIndex ?? 0,
                        WaterNewIndex = water?.NewIndex ?? 0,
                        IsInvoiceGenerated = hasInvoice,
                        ContractStartDate = startDate
                    });
                }
            }

            history = history.OrderBy(h => h.ApartmentCode).ThenByDescending(h => h.Year).ThenByDescending(h => h.Month).ToList();

            return (true, "Truy xuất dữ liệu hoàn tất.", history);
        }

        // ĐÃ NÂNG CẤP: IMPORT ĐỒNG THỜI ĐIỆN VÀ NƯỚC, ĐỌC TỪ 4 CỘT
        public async Task<(bool IsSuccess, string Message)> ImportUtilityExcelAsync(IFormFile file, int currentUserId)
        {
            if (file == null || file.Length == 0) return (false, "Tập tin đính kèm không hợp lệ.");

            ExcelPackage.License.SetNonCommercialPersonal("Sentana Project");

            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null || worksheet.Dimension == null)
                return (false, "Tập tin không chứa vùng dữ liệu hợp lệ.");

            int rowCount = worksheet.Dimension.Rows;
            int successCount = 0;
            var errors = new List<string>();

            // Cột 1: Mã Phòng, Cột 2: Điện mới, Cột 3: Nước mới, Cột 4: Ngày ghi nhận
            for (int row = 2; row <= rowCount; row++)
            {
                bool hasAptId = int.TryParse(worksheet.Cells[row, 1].Text, out int aptId);
                bool hasElec = decimal.TryParse(worksheet.Cells[row, 2].Text, out decimal elecIndex);
                bool hasWater = decimal.TryParse(worksheet.Cells[row, 3].Text, out decimal waterIndex);
                bool hasDate = DateTime.TryParse(worksheet.Cells[row, 4].Text, out DateTime regDate);

                if (hasAptId && hasDate && (hasElec || hasWater))
                {
                    if (hasElec)
                    {
                        var dtoE = new InputElectricIndexDto { ApartmentId = aptId, NewIndex = elecIndex, RegistrationDate = regDate, IsMerge = false };
                        var resE = await InputElectricityIndexAsync(dtoE, currentUserId);
                        if (resE.IsSuccess) successCount++;
                        else errors.Add($"Dòng {row} (Điện): {resE.Message}");
                    }

                    if (hasWater)
                    {
                        var dtoW = new InputWaterIndexDto { ApartmentId = aptId, NewIndex = waterIndex, RegistrationDate = regDate, IsMerge = false };
                        var resW = await InputWaterIndexAsync(dtoW, currentUserId);
                        if (resW.IsSuccess) successCount++;
                        else errors.Add($"Dòng {row} (Nước): {resW.Message}");
                    }
                }
                else
                {
                    errors.Add($"Dòng {row}: Dữ liệu sai định dạng (Thiếu ID phòng, Ngày ghi nhận hoặc không có chỉ số nào).");
                }
            }

            if (successCount == 0)
                return (false, $"Nhập dữ liệu thất bại hoàn toàn. Lỗi: {string.Join(" | ", errors.Take(2))}...");

            string warning = errors.Any() ? $" (Có {errors.Count} thao tác bị bỏ qua do lỗi. Vui lòng kiểm tra lại dữ liệu)" : "";
            return (true, $"Đã nạp thành công {successCount} bản ghi.{warning}");
        }
    }
}