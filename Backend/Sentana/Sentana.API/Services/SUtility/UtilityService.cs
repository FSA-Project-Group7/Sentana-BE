using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Utility;
using Sentana.API.Enums;
using Sentana.API.Models;
using System.Security.Claims;
using Sentana.API.Helpers;
using OfficeOpenXml;

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

            int resolvedApartmentId = 0;

            if (isManager)
            {
                if (!targetApartmentId.HasValue) return (false, "Dữ liệu đầu vào không hợp lệ. Yêu cầu cung cấp định danh căn hộ.", null);
                resolvedApartmentId = targetApartmentId.Value;
            }
            else
            {
                var contract = await _context.Contracts
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (contract == null || !contract.ApartmentId.HasValue)
                    return (false, "Hệ thống không tìm thấy hợp đồng hiệu lực.", null);

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

            return (true, "Truy xuất dữ liệu hoàn tất.", history);
        }

        public async Task<(bool IsSuccess, string Message)> ImportUtilityExcelAsync(IFormFile file, string utilityType, int currentUserId)
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

            for (int row = 2; row <= rowCount; row++)
            {
                if (int.TryParse(worksheet.Cells[row, 1].Text, out int aptId) &&
                    decimal.TryParse(worksheet.Cells[row, 2].Text, out decimal newIndex) &&
                    DateTime.TryParse(worksheet.Cells[row, 3].Text, out DateTime regDate))
                {
                    if (utilityType.ToLower() == "electric")
                    {
                        var dto = new InputElectricIndexDto { ApartmentId = aptId, NewIndex = newIndex, RegistrationDate = regDate, IsMerge = false };
                        var res = await InputElectricityIndexAsync(dto, currentUserId);
                        if (res.IsSuccess) successCount++;
                    }
                    else if (utilityType.ToLower() == "water")
                    {
                        var dto = new InputWaterIndexDto { ApartmentId = aptId, NewIndex = newIndex, RegistrationDate = regDate, IsMerge = false };
                        var res = await InputWaterIndexAsync(dto, currentUserId);
                        if (res.IsSuccess) successCount++;
                    }
                }
            }

            if (successCount == 0)
                return (false, "Xử lý hàng loạt thất bại. Dữ liệu sai định dạng hoặc vi phạm tính tuần tự.");

            return (true, $"Tiến trình hoàn tất. Xác nhận nạp {successCount} bản ghi.");
        }
    }
}