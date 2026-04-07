using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Invoice;
using Sentana.API.DTOs.Payment;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Services.SEmail;
using System;
using System.Drawing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Sentana.API.Services.SInvoice
{
    public class InvoiceService : IInvoiceService
    {
        private readonly SentanaContext _context;
        private readonly IEmailService _emailService;

        public InvoiceService(SentanaContext context, IEmailService emailService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _emailService = emailService;
        }

        // View monthly invoice 
        public async Task<List<InvoiceResponseDto>> GetCurrentInvoicesAsync(ClaimsPrincipal user, int? month = null, int? year = null, int? apartmentId = null, int? accountId = null)
        {
            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                throw new UnauthorizedAccessException("Xác thực danh tính thất bại.");

            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

            var targetApartmentIds = new List<int>();

            if (isManager)
            {
                if (apartmentId.HasValue) targetApartmentIds.Add(apartmentId.Value);
                else if (accountId.HasValue)
                {
                    var aptIds = await _context.Contracts
                        .Where(c => c.AccountId == accountId.Value && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                        .Select(c => c.ApartmentId)
                        .Where(id => id.HasValue).Select(id => id!.Value).ToListAsync();
                    targetApartmentIds.AddRange(aptIds);
                }
            }
            else
            {
                var aptIds = await _context.Contracts
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .Select(c => c.ApartmentId)
                    .Where(id => id.HasValue).Select(id => id!.Value).ToListAsync();
                targetApartmentIds.AddRange(aptIds);
            }

            if (!targetApartmentIds.Any()) return new List<InvoiceResponseDto>();

            var query = _context.Invoices
                .Include(i => i.Apartment)
                .Include(i => i.Contract)
                .Include(i => i.ElectricMeter)
                .Include(i => i.WaterMeter)
                .Where(i => targetApartmentIds.Contains(i.ApartmentId.Value) && i.IsDeleted == false);

            if (month.HasValue && year.HasValue)
            {
                query = query.Where(i => i.BillingMonth == month.Value && i.BillingYear == year.Value);
            }

            var rawInvoices = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

            var invoicesToProcess = month.HasValue && year.HasValue
                ? rawInvoices
                : rawInvoices.GroupBy(i => i.ApartmentId).Select(g => g.First()).ToList();

            var result = new List<InvoiceResponseDto>();

            foreach (var invoice in invoicesToProcess)
            {
                int gap = 1;
                string billingPeriodStr = $"Tháng {invoice.BillingMonth}/{invoice.BillingYear}";

                // Thuật toán truy hồi xác định hệ số gộp kỳ
                if (invoice.Contract != null && invoice.Contract.StartDay.HasValue)
                {
                    DateTime contractStartDate = invoice.Contract.StartDay.Value.ToDateTime(TimeOnly.MinValue);
                    int currentMonthId = (invoice.BillingYear ?? 0) * 12 + (invoice.BillingMonth ?? 0);

                    var prevInvoice = await _context.Invoices
                        .Where(i => i.ApartmentId == invoice.ApartmentId && i.IsDeleted == false && (i.BillingYear * 12 + i.BillingMonth) < currentMonthId)
                        .OrderByDescending(i => i.BillingYear * 12 + i.BillingMonth)
                        .FirstOrDefaultAsync();

                    int lastMonthId = prevInvoice != null
                        ? (prevInvoice.BillingYear ?? 0) * 12 + (prevInvoice.BillingMonth ?? 0)
                        : (contractStartDate.Year * 12 + contractStartDate.Month) - 1;

                    gap = currentMonthId - lastMonthId;
                    if (gap < 1) gap = 1;

                    if (gap > 1)
                    {
                        int startMonthId = lastMonthId + 1;
                        int startYear = startMonthId / 12;
                        int startMonth = startMonthId % 12;
                        if (startMonth == 0) { startMonth = 12; startYear--; }

                        billingPeriodStr = startYear < invoice.BillingYear
                            ? $"Tháng {startMonth}/{startYear} - {invoice.BillingMonth}/{invoice.BillingYear}"
                            : $"Tháng {startMonth} - {invoice.BillingMonth} / {invoice.BillingYear}";
                    }
                }

                var dto = new InvoiceResponseDto
                {
                    InvoiceId = invoice.InvoiceId,
                    ApartmentId = invoice.ApartmentId,
                    ApartmentCode = invoice.Apartment?.ApartmentCode,
                    ContractId = invoice.ContractId,
                    BillingMonth = invoice.BillingMonth,
                    BillingYear = invoice.BillingYear,
                    BillingPeriod = billingPeriodStr, // Gán chuỗi kỳ thanh toán đã xử lý
                    TotalMoney = invoice.TotalMoney,
                    ServiceFee = invoice.ServiceFee,
                    Pay = invoice.Pay,
                    Debt = invoice.Debt,
                    WaterNumber = invoice.WaterNumber,
                    ElectricNumber = invoice.ElectricNumber,
                    DayCreat = invoice.CreatedAt?.ToString("dd/MM/yyyy HH:mm"), // Sử dụng CreatedAt thay vì DayCreat rỗng
                    DayPay = invoice.DayPay?.ToString("dd/MM/yyyy"),
                    StatusName = invoice.Status?.ToString() ?? string.Empty,
                    Payments = invoice.Payments,
                    Details = new List<InvoiceDetailItemDto>()
                };

                decimal calculatedBaseTotal = 0m;

                if (invoice.Contract != null)
                {
                    decimal rent = (invoice.Contract.MonthlyRent ?? 0) * gap;
                    string rentName = gap > 1 ? $"Tiền thuê phòng (Gộp {gap} tháng)" : "Tiền thuê phòng";
                    dto.Details.Add(new InvoiceDetailItemDto { FeeName = rentName, Amount = rent });
                    calculatedBaseTotal += rent;
                }

                var elecUsage = invoice.ElectricNumber ?? 0;
                if (elecUsage > 0 || invoice.ElectricMeter != null)
                {
                    var price = invoice.ElectricMeter?.Price ?? 3500m;
                    decimal elecMoney = elecUsage * price;
                    dto.Details.Add(new InvoiceDetailItemDto { FeeName = $"Tiền điện ({elecUsage:0.##} kWh)", Amount = elecMoney });
                    calculatedBaseTotal += elecMoney;
                }

                var waterUsage = invoice.WaterNumber ?? 0;
                if (waterUsage > 0 || invoice.WaterMeter != null)
                {
                    var price = invoice.WaterMeter?.Price ?? 25000m;
                    decimal waterMoney = waterUsage * price;
                    dto.Details.Add(new InvoiceDetailItemDto { FeeName = $"Tiền nước ({waterUsage:0.##} khối)", Amount = waterMoney });
                    calculatedBaseTotal += waterMoney;
                }

                decimal serviceFee = (invoice.ServiceFee ?? 0);
                string serviceName = gap > 1 ? $"Phí dịch vụ (Gộp {gap} tháng)" : "Phí dịch vụ";
                dto.Details.Add(new InvoiceDetailItemDto { FeeName = serviceName, Amount = serviceFee });
                calculatedBaseTotal += serviceFee;

                // Bóc tách Phụ phí: Nếu Tổng tiền Hóa đơn > Tổng 4 loại phí cơ bản, phần chênh lệch chính là Phụ phí
                if (invoice.TotalMoney > calculatedBaseTotal)
                {
                    decimal additionalFee = (invoice.TotalMoney ?? 0) - calculatedBaseTotal;

                    string feeReason = !string.IsNullOrWhiteSpace(invoice.Note)
                        ? $"Phụ phí ({invoice.Note})"
                        : "Phụ phí / Nợ phát sinh";

                    dto.Details.Add(new InvoiceDetailItemDto { FeeName = feeReason, Amount = additionalFee });
                }

                result.Add(dto);
            }

            return result;
        }

        // gen hóa đơn hàng tháng
        public async Task<(bool IsSuccess, string Message, int GeneratedCount)> GenerateMonthlyInvoicesAsync(GenerateInvoiceRequestDto request, int currentUserId)
        {
            var validation = ValidationHelper.ValidateMonthYear(request.Month, request.Year);
            if (!validation.IsValid) return (false, validation.ErrorMessage, 0);

            int targetMonth = request.Month;
            int targetYear = request.Year;

            var query = _context.Apartments.Where(a => a.Status == ApartmentStatus.Occupied && a.IsDeleted == false);

            if (request.ApartmentId.HasValue)
                query = query.Where(a => a.ApartmentId == request.ApartmentId.Value);

            var activeApartments = await query.ToListAsync();
            if (!activeApartments.Any()) return (false, "Hệ thống không tìm thấy căn hộ đáp ứng điều kiện phát hành hóa đơn.", 0);

            int generatedCount = 0;
            int alreadyExistsCount = 0;
            int invalidTimeCount = 0;
            int lockedPastCount = 0;
            int exceedMergeLimitCount = 0;
            int missingUtilityCount = 0;

            var emailTasks = new List<Task>();

            foreach (var apt in activeApartments)
            {
                var contract = await _context.Contracts
                    .Include(c => c.Account)
                    .Where(c => c.ApartmentId == apt.ApartmentId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (contract == null || !contract.StartDay.HasValue) continue;

                DateTime contractStartDate = contract.StartDay.Value.ToDateTime(TimeOnly.MinValue);

                int requestMonthId = targetYear * 12 + targetMonth;
                int contractStartMonthId = contractStartDate.Year * 12 + contractStartDate.Month;

                if (requestMonthId < contractStartMonthId)
                {
                    invalidTimeCount++;
                    continue;
                }

                var latestInvoice = await _context.Invoices
                    .Where(i => i.ApartmentId == apt.ApartmentId && i.IsDeleted == false)
                    .OrderByDescending(i => (i.BillingYear ?? 0) * 12 + (i.BillingMonth ?? 0))
                    .FirstOrDefaultAsync();

                int lastInvoiceMonthId = latestInvoice != null
                    ? (latestInvoice.BillingYear ?? 0) * 12 + (latestInvoice.BillingMonth ?? 0)
                    : contractStartMonthId - 1;

                if (requestMonthId <= lastInvoiceMonthId)
                {
                    if (requestMonthId == lastInvoiceMonthId) alreadyExistsCount++;
                    else lockedPastCount++;
                    continue;
                }

                int gap = requestMonthId - lastInvoiceMonthId;
                if (gap > 3)
                {
                    exceedMergeLimitCount++;
                    continue;
                }

                var elecMeterCurrent = await _context.ElectricMeters
                    .FirstOrDefaultAsync(e => e.ApartmentId == apt.ApartmentId && e.RegistrationDate.HasValue && e.RegistrationDate.Value.Month == targetMonth && e.RegistrationDate.Value.Year == targetYear && e.IsDeleted == false);

                var waterMeterCurrent = await _context.WaterMeters
                    .FirstOrDefaultAsync(w => w.ApartmentId == apt.ApartmentId && w.RegistrationDate.HasValue && w.RegistrationDate.Value.Month == targetMonth && w.RegistrationDate.Value.Year == targetYear && w.IsDeleted == false);

                if (elecMeterCurrent == null || waterMeterCurrent == null)
                {
                    missingUtilityCount++;
                    continue;
                }

                var unbilledElecs = await _context.ElectricMeters
                    .Where(e => e.ApartmentId == apt.ApartmentId && e.IsDeleted == false &&
                                e.RegistrationDate.HasValue &&
                                (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) > lastInvoiceMonthId &&
                                (e.RegistrationDate.Value.Year * 12 + e.RegistrationDate.Value.Month) <= requestMonthId)
                    .ToListAsync();

                var unbilledWaters = await _context.WaterMeters
                    .Where(w => w.ApartmentId == apt.ApartmentId && w.IsDeleted == false &&
                                w.RegistrationDate.HasValue &&
                                (w.RegistrationDate.Value.Year * 12 + w.RegistrationDate.Value.Month) > lastInvoiceMonthId &&
                                (w.RegistrationDate.Value.Year * 12 + w.RegistrationDate.Value.Month) <= requestMonthId)
                    .ToListAsync();

                decimal elecConsumption = unbilledElecs.Sum(e => (e.NewIndex ?? 0) - (e.OldIndex ?? 0));
                decimal elecMoney = unbilledElecs.Sum(e => ((e.NewIndex ?? 0) - (e.OldIndex ?? 0)) * (e.Price ?? 3500m));

                decimal waterConsumption = unbilledWaters.Sum(w => (w.NewIndex ?? 0) - (w.OldIndex ?? 0));
                decimal waterMoney = unbilledWaters.Sum(w => ((w.NewIndex ?? 0) - (w.OldIndex ?? 0)) * (w.Price ?? 25000m));

                decimal rentAmount = contract.MonthlyRent ?? 0m;
                decimal totalRent = rentAmount * gap;

                var services = await _context.ApartmentServices
                    .Where(s => s.ApartmentId == apt.ApartmentId && s.Status == GeneralStatus.Active && s.IsDeleted == false)
                    .ToListAsync();

                decimal serviceFeePerMonth = services.Sum(s => s.ActualPrice ?? 0m);
                decimal totalServiceFee = serviceFeePerMonth * gap;

                decimal totalAmount = totalRent + elecMoney + waterMoney + totalServiceFee;

                var invoice = new Invoice
                {
                    ApartmentId = apt.ApartmentId,
                    ContractId = contract.ContractId,
                    ElectricMeterId = elecMeterCurrent.ElectricMeterId,
                    WaterMeterId = waterMeterCurrent.WaterMeterId,
                    BillingMonth = targetMonth,
                    BillingYear = targetYear,
                    ElectricNumber = elecConsumption,
                    WaterNumber = waterConsumption,
                    ServiceFee = totalServiceFee,
                    TotalMoney = totalAmount,
                    Pay = 0,
                    Debt = totalAmount,
                    Status = InvoiceStatus.Unpaid,
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUserId
                };

                _context.Invoices.Add(invoice);
                generatedCount++;

                if (contract.AccountId.HasValue)
                {
                    var notification = new Notification
                    {
                        AccountId = contract.AccountId.Value,
                        Title = "Thông báo phát hành hóa đơn",
                        Message = gap > 1
                            ? $"Hóa đơn kỳ {targetMonth}/{targetYear} (bao gồm gộp {gap} kỳ) đã được phát hành cho căn hộ {apt.ApartmentCode}."
                            : $"Hóa đơn kỳ {targetMonth}/{targetYear} đã được phát hành cho căn hộ {apt.ApartmentCode}.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notification);

                    if (contract.Account != null && !string.IsNullOrEmpty(contract.Account.Email))
                    {
                        string residentName = contract.Account.UserName ?? "Quý khách";
                        string mergeText = gap > 1 ? $"<p style='color:#dc3545;'>Ghi chú: Hóa đơn này đã được tính toán gộp chi phí của {gap} kỳ chưa thanh toán.</p>" : "";
                        string emailSubject = $"[SENTANA] Thông báo cước phí dịch vụ kỳ {targetMonth}/{targetYear}";
                        string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                        <h2 style='color: #00c292;'>THÔNG BÁO CƯỚC PHÍ DỊCH VỤ</h2>
                        <p>Kính gửi {residentName} (Căn hộ {apt.ApartmentCode}),</p>
                        <p>Hóa đơn dịch vụ kỳ <strong>{targetMonth}/{targetYear}</strong> đã được hệ thống ghi nhận.</p>
                        {mergeText}
                        <p>Tổng dư nợ cần thanh toán: <strong style='color:#dc3545; font-size: 1.2em;'>{totalAmount:N0} VNĐ</strong>.</p>
                        <p>Quý khách vui lòng kiểm tra chi tiết và thực hiện thanh toán qua Cổng thông tin Cư dân Sentana.</p>
                        <br/>
                        <p>Trân trọng,<br/>Ban Quản Lý Tòa Nhà Sentana.</p>
                    </div>";

                        emailTasks.Add(_emailService.SendEmailAsync(contract.Account.Email, emailSubject, emailBody));
                    }
                }
            }

            var msgParts = new List<string>();
            if (alreadyExistsCount > 0) msgParts.Add($"{alreadyExistsCount} phòng đã ban hành");
            if (missingUtilityCount > 0) msgParts.Add($"{missingUtilityCount} phòng khuyết chỉ số tiêu thụ");
            if (lockedPastCount > 0) msgParts.Add($"{lockedPastCount} phòng bị khóa truy xuất quá khứ");
            if (exceedMergeLimitCount > 0) msgParts.Add($"{exceedMergeLimitCount} phòng vượt giới hạn gộp (tối đa 3 tháng)");
            if (invalidTimeCount > 0) msgParts.Add($"{invalidTimeCount} phòng chưa tới hạn hợp đồng");

            string skipSummary = msgParts.Any() ? $" Trạng thái loại trừ: {string.Join(", ", msgParts)}." : "";

            if (generatedCount > 0)
            {
                try
                {
                    await _context.SaveChangesAsync();

                    if (emailTasks.Any())
                    {
                        _ = Task.WhenAll(emailTasks);
                    }

                    return (true, $"Tiến trình hoàn tất. Đã ban hành {generatedCount} hóa đơn.{skipSummary}", generatedCount);
                }
                catch (DbUpdateException)
                {
                    return (false, "Xung đột tiến trình. Vui lòng kiểm tra lại thao tác.", 0);
                }
            }

            return (false, $"Tiến trình bị gián đoạn. Không có hóa đơn hợp lệ để ban hành.{skipSummary}", 0);
        }

        // View invoice list 
        public async Task<PagedResult<InvoiceListItemDto>> GetInvoiceListAsync(InvoiceListRequestDto request)
        {
            var query = _context.Invoices
                .Include(i => i.Apartment)
                .Include(i => i.Contract)
                .Where(i => i.IsDeleted == false);

            if (request.Month.HasValue) query = query.Where(i => i.BillingMonth == request.Month.Value);
            if (request.Year.HasValue) query = query.Where(i => i.BillingYear == request.Year.Value);
            if (request.Status.HasValue) query = query.Where(i => i.Status == request.Status.Value);

            int totalCount = await query.CountAsync();

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var items = new List<InvoiceListItemDto>();

            foreach (var invoice in invoices)
            {
                int gap = 1;
                string billingPeriodStr = $"Tháng {invoice.BillingMonth}/{invoice.BillingYear}";

                if (invoice.Contract != null && invoice.Contract.StartDay.HasValue)
                {
                    DateTime contractStartDate = invoice.Contract.StartDay.Value.ToDateTime(TimeOnly.MinValue);
                    int currentMonthId = (invoice.BillingYear ?? 0) * 12 + (invoice.BillingMonth ?? 0);

                    var prevInvoice = await _context.Invoices
                        .Where(i => i.ApartmentId == invoice.ApartmentId && i.IsDeleted == false && (i.BillingYear * 12 + i.BillingMonth) < currentMonthId)
                        .OrderByDescending(i => i.BillingYear * 12 + i.BillingMonth)
                        .FirstOrDefaultAsync();

                    int lastMonthId = prevInvoice != null
                        ? (prevInvoice.BillingYear ?? 0) * 12 + (prevInvoice.BillingMonth ?? 0)
                        : (contractStartDate.Year * 12 + contractStartDate.Month) - 1;

                    gap = currentMonthId - lastMonthId;
                    if (gap < 1) gap = 1;

                    if (gap > 1)
                    {
                        int startMonthId = lastMonthId + 1;
                        int startYear = startMonthId / 12;
                        int startMonth = startMonthId % 12;
                        if (startMonth == 0) { startMonth = 12; startYear--; }

                        billingPeriodStr = startYear < invoice.BillingYear
                            ? $"Tháng {startMonth}/{startYear} - {invoice.BillingMonth}/{invoice.BillingYear}"
                            : $"Tháng {startMonth} - {invoice.BillingMonth}/{invoice.BillingYear}";
                    }
                }

                items.Add(new InvoiceListItemDto
                {
                    InvoiceId = invoice.InvoiceId,
                    ApartmentId = invoice.ApartmentId,
                    ApartmentCode = invoice.Apartment?.ApartmentCode,
                    BillingMonth = invoice.BillingMonth,
                    BillingYear = invoice.BillingYear,
                    BillingPeriod = billingPeriodStr,
                    TotalMoney = invoice.TotalMoney,
                    Debt = invoice.Debt,
                    StatusName = invoice.Status?.ToString() ?? string.Empty,
                    CreatedAt = invoice.CreatedAt?.ToString("dd/MM/yyyy HH:mm")
                });
            }

            return new PagedResult<InvoiceListItemDto>
            {
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                Items = items
            };
        }

        // Edit invoice
        public async Task<(bool IsSuccess, string Message)> EditInvoiceAsync(int invoiceId, EditInvoiceDto request)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.IsDeleted == false);

            if (invoice == null) return (false, "Hệ thống không tìm thấy hóa đơn.");
            if (invoice.Status != InvoiceStatus.Unpaid) return (false, "Chỉ được chỉnh sửa khi hóa đơn chưa thanh toán.");

            if (request.AdditionalFee.HasValue && request.AdditionalFee.Value > 0)
            {
                invoice.TotalMoney += request.AdditionalFee.Value;
                invoice.Debt += request.AdditionalFee.Value;

                // Ghi nhận lý do phụ phí
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    invoice.Note = request.Note;
                }
            }

            _context.Invoices.Update(invoice);
            bool isSaved = await _context.SaveChangesAsync() > 0;

            return isSaved ? (true, "Cập nhật hóa đơn thành công.") : (false, "Phát sinh lỗi trong quá trình lưu trữ.");
        }

        // Gửi email nhắc nợ 
        public async Task<(bool IsSuccess, string Message)> SendInvoiceNotificationAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Contract)
                    .ThenInclude(c => c.Account)
                        .ThenInclude(a => a.Info)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.IsDeleted == false);

            if (invoice == null) return (false, "Không tìm thấy hóa đơn.");

            if ((int)invoice.Status == 0)
                return (false, "Hóa đơn đang ở trạng thái Nháp, chưa được ban hành. Không thể gửi thông báo.");

            if (invoice.Status == InvoiceStatus.Paid || invoice.Pay >= invoice.TotalMoney || invoice.Debt <= 0)
            {
                return (false, "Hóa đơn đã được thanh toán. Không thể gửi thông báo.");
            }

            var account = invoice.Contract?.Account;
            if (account == null || string.IsNullOrEmpty(account.Email))
                return (false, "Không tìm thấy thông tin Email của chủ hộ để gửi.");

            string residentName = account.Info?.FullName ?? account.UserName ?? "Quý khách";

            // Template Mail
            string subject = $"[SENTANA] Thông báo cước phí tháng {invoice.BillingMonth}/{invoice.BillingYear}";
            string body = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 8px; max-width: 600px;'>
                <h2 style='color: #122240; border-bottom: 2px solid #00c292; padding-bottom: 10px;'>THÔNG BÁO HÓA ĐƠN ĐỊNH KỲ</h2>
                <p>Kính gửi <strong>{residentName}</strong>,</p>
                <p>Ban quản lý Sentana xin gửi thông báo cước phí dịch vụ tháng <strong>{invoice.BillingMonth}/{invoice.BillingYear}</strong> của quý khách.</p>
                <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #dc3545; margin: 20px 0;'>
                    <h3 style='color: #dc3545; margin: 0;'>Tổng số tiền: {invoice.TotalMoney:N0} VNĐ</h3>
                    <p style='margin: 5px 0 0 0;'>Trạng thái: <strong>Chưa thanh toán</strong></p>
                </div>
                <p>Quý khách vui lòng đăng nhập vào <b>Cổng thông tin Cư dân Sentana</b> để xem chi tiết và tải lên biên lai chuyển khoản.</p>
                <br/>
                <p>Trân trọng,<br/><strong>Ban Quản Lý Tòa Nhà Sentana</strong></p>
            </div>";

            await _emailService.SendEmailAsync(account.Email, subject, body);

            return (true, "Đã gửi email thông báo nhắc nợ thành công.");
        }

        // Approve payment
        public async Task<(bool IsSuccess, string Message)> ApprovePaymentAsync(int transactionId, int currentUserId)
        {
            var transaction = await _context.PaymentTransactions
                .Include(t => t.Invoice)
                    .ThenInclude(i => i.Contract)
                        .ThenInclude(c => c.Account)
                            .ThenInclude(a => a.Info)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.IsDeleted == false);

            if (transaction == null) return (false, "Không tìm thấy giao dịch thanh toán này.");
            if (transaction.Status != PaymentTransactionStatus.Pending)
                return (false, "Giao dịch này đã được xử lý trước đó.");

            // Cập nhật bảng PaymentTransaction
            transaction.Status = PaymentTransactionStatus.Approved;
            transaction.UpdatedBy = currentUserId;
            transaction.UpdatedAt = DateTime.Now;

            if (transaction.Invoice != null)
            {
                // Cập nhật bảng Invoice
                transaction.Invoice.Status = InvoiceStatus.Paid;
                transaction.Invoice.DayPay = DateOnly.FromDateTime(DateTime.Now);
                transaction.Invoice.Pay = transaction.AmountPaid;
                transaction.Invoice.Debt = 0;

                _context.Invoices.Update(transaction.Invoice);

                // Tạo thông báo
                if (transaction.Invoice.Contract?.AccountId != null)
                {
                    var notification = new Notification
                    {
                        AccountId = transaction.Invoice.Contract.AccountId.Value,
                        Title = "Thanh toán thành công",
                        Message = $"Giao dịch thanh toán {transaction.AmountPaid:N0} VNĐ cho hóa đơn tháng {transaction.Invoice.BillingMonth}/{transaction.Invoice.BillingYear} đã được xác nhận.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notification);
                }
            }

            // Xử lý đồng thời
            bool isSaved = false;
            try
            {
                isSaved = await _context.SaveChangesAsync() > 0;
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "Giao dịch này vừa được duyệt hoặc từ chối bởi một người khác. Vui lòng tải lại trang để xem trạng thái mới nhất.");
            }

            if (isSaved && transaction.Invoice?.Contract?.Account?.Email != null)
            {
                var account = transaction.Invoice.Contract.Account;
                string residentName = account.Info?.FullName ?? account.UserName ?? "Quý khách";
                string subject = "[SENTANA] Xác nhận thanh toán thành công";
                string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 15px; border: 1px solid #28a745; border-radius: 8px;'>
                    <h3 style='color: #28a745; margin-top: 0;'>XÁC NHẬN THANH TOÁN</h3>
                    <p>Kính gửi <strong>{residentName}</strong>,</p>
                    <p>Ban quản lý đã xác nhận khoản thanh toán <strong style='color:#28a745;'>{transaction.AmountPaid:N0} VNĐ</strong> cho hóa đơn tháng {transaction.Invoice.BillingMonth}/{transaction.Invoice.BillingYear}.</p>
                    <p>Cảm ơn quý khách đã thanh toán đúng hạn!</p>
                    <p style='font-size: 0.9em; color: #555;'>Trân trọng,<br/>Ban Quản Lý Tòa Nhà Sentana.</p>
                </div>";

                // Chạy ngầm (Fire-and-forget)
                _ = _emailService.SendEmailAsync(account.Email, subject, body);
            }

            return isSaved ? (true, "Đã duyệt thanh toán thành công. Hóa đơn chuyển sang Đã thanh toán.")
                           : (false, "Lỗi hệ thống khi lưu dữ liệu.");
        }

        // Reject payment
        public async Task<(bool IsSuccess, string Message)> RejectPaymentAsync(int transactionId, RejectPaymentDto request, int currentUserId)
        {
            var transaction = await _context.PaymentTransactions
                .Include(t => t.Invoice)
                    .ThenInclude(i => i.Contract)
                        .ThenInclude(c => c.Account)
                            .ThenInclude(a => a.Info)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.IsDeleted == false);

            if (transaction == null) return (false, "Không tìm thấy giao dịch thanh toán này.");
            if (transaction.Status != PaymentTransactionStatus.Pending)
                return (false, "Giao dịch này đã được xử lý trước đó.");

            transaction.Status = PaymentTransactionStatus.Rejected;
            transaction.Note = request.Reason;
            transaction.UpdatedBy = currentUserId;
            transaction.UpdatedAt = DateTime.Now;

            if (transaction.Invoice != null)
            {
                // Cập nhật Invoice trả về trạng thái Unpaid
                transaction.Invoice.Status = InvoiceStatus.Unpaid;

                // Tạo notification
                if (transaction.Invoice.Contract?.AccountId != null)
                {
                    var notification = new Notification
                    {
                        AccountId = transaction.Invoice.Contract.AccountId.Value,
                        Title = "Giao dịch bị từ chối",
                        Message = $"Giao dịch thanh toán hóa đơn tháng {transaction.Invoice.BillingMonth}/{transaction.Invoice.BillingYear} bị từ chối. Lý do: {request.Reason}.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notification);
                }
            }

            bool isSaved = await _context.SaveChangesAsync() > 0;

            if (isSaved && transaction.Invoice?.Contract?.Account?.Email != null)
            {
                var account = transaction.Invoice.Contract.Account;
                string residentName = account.Info?.FullName ?? account.UserName ?? "Quý khách";
                string subject = "[SENTANA] Lỗi giao dịch thanh toán hóa đơn";
                string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 15px; border: 1px solid #dc3545; border-radius: 8px;'>
                    <h3 style='color: #dc3545; margin-top: 0;'>GIAO DỊCH BỊ TỪ CHỐI</h3>
                    <p>Kính gửi <strong>{residentName}</strong>,</p>
                    <p>Biên lai thanh toán cho hóa đơn tháng {transaction.Invoice.BillingMonth}/{transaction.Invoice.BillingYear} hiện <strong>chưa được chấp nhận</strong>.</p>
                    <p>Lý do từ chối: <strong style='color:red;'>{request.Reason}</strong></p>
                    <p>Quý khách vui lòng đăng nhập vào ứng dụng Cổng cư dân để tải lại biên lai hợp lệ.</p>
                    <p style='font-size: 0.9em; color: #555;'>Trân trọng,<br/>Ban Quản Lý Tòa Nhà Sentana.</p>
                </div>";

                // Chạy ngầm (Fire-and-forget) 
                _ = _emailService.SendEmailAsync(account.Email, subject, body);
            }

            return isSaved ? (true, "Đã từ chối thanh toán thành công.")
                           : (false, "Lỗi hệ thống khi lưu dữ liệu.");
        }

        // US82 - View Outstanding Debt
        public async Task<List<OutstandingDebtItemDto>> GetOutstandingDebtsAsync()
        {
            var overdueInvoices = await _context.Invoices
                .Include(i => i.Apartment)
                .Where(i =>
                    i.IsDeleted == false &&
                    (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PendingVerification) &&
                    i.Debt > 0)
                // Sắp xếp theo ngày tạo (Hóa đơn cũ nhất lên đầu)
                .OrderBy(i => i.CreatedAt)
                .ToListAsync();

            var today = DateTime.Today;

            return overdueInvoices.Select(i => new OutstandingDebtItemDto
            {
                InvoiceId = i.InvoiceId,
                ApartmentId = i.ApartmentId,
                ApartmentCode = i.Apartment?.ApartmentCode,
                ApartmentName = i.Apartment?.ApartmentName,
                BillingMonth = i.BillingMonth,
                BillingYear = i.BillingYear,
                TotalMoney = i.TotalMoney,
                Debt = i.Debt,
                DayPay = i.DayPay, // Chắc chắn sẽ là null vì chưa thanh toán
                // Tính số ngày nợ dựa trên ngày tạo hóa đơn
                DaysOverdue = i.CreatedAt.HasValue ? (today - i.CreatedAt.Value.Date).Days : 0,
                Status = i.Status.HasValue ? i.Status.Value.ToString() : null
            }).ToList();
        }

        // US83 - Export Debt Report (Xuất báo cáo công nợ ra Excel)
        public async Task<byte[]> ExportDebtReportAsync()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Sentana");

            // 1. Lọc hóa đơn CHUẨN logic Outstanding Debt (Bỏ điều kiện DayPay)
            var unpaidInvoices = await _context.Invoices
                .Include(i => i.Apartment)
                .Include(i => i.Contract)
                    .ThenInclude(c => c.Account)
                        .ThenInclude(a => a.Info)
                .Where(i => i.IsDeleted == false &&
                            (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PendingVerification) &&
                            i.Debt > 0)
                .ToListAsync();

            // 2. Nhóm dữ liệu theo Căn hộ + Chủ tài khoản (Tránh gộp nhầm nợ chủ cũ/mới)
            var exportData = unpaidInvoices
                .GroupBy(i => new
                {
                    i.ApartmentId,
                    ApartmentCode = i.Apartment?.ApartmentCode,
                    AccountId = i.Contract?.AccountId, // Điểm mấu chốt để phân biệt
                    ResidentName = i.Contract?.Account?.Info?.FullName
                                   ?? i.Contract?.Account?.UserName
                                   ?? "Không xác định"
                })
                .Select(g => new
                {
                    RoomNumber = g.Key.ApartmentCode ?? "N/A",
                    ResidentName = g.Key.ResidentName,
                    TotalAmountOwed = g.Sum(i => i.Debt ?? 0)
                })
                .OrderBy(x => x.RoomNumber)
                .ToList();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Debt Report");

            // Khởi tạo Header
            worksheet.Cells[1, 1].Value = "Mã Phòng (Room Number)";
            worksheet.Cells[1, 2].Value = "Tên Cư Dân (Resident Name)";
            worksheet.Cells[1, 3].Value = "Tổng Nợ (Total Amount Owed)";

            using (var range = worksheet.Cells[1, 1, 1, 3])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // 3. Xử lý trường hợp Không có ai nợ
            if (!exportData.Any())
            {
                worksheet.Cells[2, 1, 2, 3].Merge = true;
                worksheet.Cells[2, 1].Value = "Hiện tại không có cư dân nào đang nợ đọng/quá hạn.";
                worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[2, 1].Style.Font.Italic = true;

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                return await package.GetAsByteArrayAsync();
            }

            // Đổ dữ liệu vào các dòng
            int row = 2;
            foreach (var item in exportData)
            {
                worksheet.Cells[row, 1].Value = item.RoomNumber;
                worksheet.Cells[row, 2].Value = item.ResidentName;

                worksheet.Cells[row, 3].Value = item.TotalAmountOwed;
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0 VNĐ";

                row++;
            }

            // Tự động căn chỉnh độ rộng
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return await package.GetAsByteArrayAsync();
        }

        // US73 - Change Invoice Status 
        public async Task<(bool IsSuccess, string Message)> ChangeInvoiceStatusAsync(int invoiceId, ChangeInvoiceStatusDto request, int currentUserId)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.IsDeleted == false);

            if (invoice == null)
                return (false, "Hệ thống không tìm thấy hóa đơn này.");

            if (invoice.Status == request.Status)
                return (false, "Hóa đơn đã đang ở trạng thái này, không cần thay đổi.");

            if (invoice.Status == InvoiceStatus.Paid)
                return (false, "Hóa đơn này đã được thanh toán hoàn tất, không thể thay đổi trạng thái.");

            if (request.Status == InvoiceStatus.Unpaid && string.IsNullOrWhiteSpace(request.Note))
                return (false, "Bắt buộc phải nhập Lý do (Ghi chú) khi chuyển hóa đơn về trạng thái Chưa thanh toán.");

            invoice.Status = request.Status;

            if (request.Status == InvoiceStatus.Paid)
            {
                invoice.Pay = invoice.TotalMoney;
                invoice.Debt = 0;
                invoice.DayPay = DateOnly.FromDateTime(DateTime.Now);
            }
            else if (request.Status == InvoiceStatus.Unpaid)
            {
                invoice.Pay = 0;
                invoice.Debt = invoice.TotalMoney;
                invoice.DayPay = null;
            }

            // Ghi chú nội bộ 
            string? cleanNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            if (!string.IsNullOrEmpty(cleanNote))
            {
                string newLog = $"[{DateTime.Now:dd/MM/yy HH:mm}] {cleanNote}";

                string combinedNote = string.IsNullOrWhiteSpace(invoice.ManagerNote)
                    ? newLog
                    : $"{invoice.ManagerNote} | {newLog}";

                invoice.ManagerNote = combinedNote.Length > 500
                    ? "..." + combinedNote.Substring(combinedNote.Length - 497)
                    : combinedNote;
            }

            invoice.UpdatedAt = DateTime.Now;
            invoice.UpdatedBy = currentUserId;

            try
            {
                _context.Invoices.Update(invoice);
                await _context.SaveChangesAsync();
                return (true, "Đã cập nhật trạng thái hóa đơn thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, "Hóa đơn này vừa được cập nhật bởi một người khác. Vui lòng tải lại trang để xem dữ liệu mới nhất.");
            }
        }

        // US81 - View Monthly Revenue (Manager) — scoped to manager's buildings
        public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int managerId, int? year)
        {
            if (managerId <= 0) return new List<MonthlyRevenueDto>();

            var targetYear = year ?? DateTime.Now.Year;

            // Validate năm hợp lệ (không cho năm quá xa trong tương lai hoặc quá khứ)
            if (targetYear < 2000 || targetYear > DateTime.Now.Year + 1)
                return new List<MonthlyRevenueDto>();

            // Lấy danh sách ApartmentId thuộc manager
            var managerApartmentIds = await _context.Buildings
                .Where(b => b.ManagerId == managerId && b.IsDeleted != true)
                .SelectMany(b => b.Apartments)
                .Where(a => a.IsDeleted != true)
                .Select(a => a.ApartmentId)
                .ToListAsync();

            // Early return nếu manager không có apartment nào
            if (!managerApartmentIds.Any()) return new List<MonthlyRevenueDto>();

            var invoices = await _context.Invoices
                .Where(i => i.IsDeleted != true
                         && i.BillingYear == targetYear
                         && managerApartmentIds.Contains(i.ApartmentId ?? 0))
                .ToListAsync();

            var grouped = invoices
                .GroupBy(i => new { i.BillingMonth, i.BillingYear })
                .OrderBy(g => g.Key.BillingYear)
                .ThenBy(g => g.Key.BillingMonth)
                .Select(g => new MonthlyRevenueDto
                {
                    Month = g.Key.BillingMonth ?? 0,
                    Year = g.Key.BillingYear ?? targetYear,
                    TotalBilled = g.Sum(i => i.TotalMoney ?? 0),
                    TotalCollected = g.Sum(i => i.Pay ?? 0),
                    TotalDebt = g.Sum(i => i.Debt ?? 0),
                    TotalInvoices = g.Count(),
                    PaidInvoices = g.Count(i => i.Status == InvoiceStatus.Paid),
                    UnpaidInvoices = g.Count(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PendingVerification)
                })
                .ToList();

            return grouped;
        }

        // US14 - View Payment Statistics (Manager) — scoped to manager's buildings
        public async Task<PaymentStatisticsDto> GetPaymentStatisticsAsync(int managerId, int? month, int? year)
        {
            // Lấy danh sách ApartmentId thuộc manager
            var managerApartmentIds = await _context.Buildings
                .Where(b => b.ManagerId == managerId && b.IsDeleted == false)
                .SelectMany(b => b.Apartments)
                .Where(a => a.IsDeleted == false)
                .Select(a => a.ApartmentId)
                .ToListAsync();

            var query = _context.Invoices
                .Include(i => i.Apartment)
                .Where(i => i.IsDeleted == false
                         && managerApartmentIds.Contains(i.ApartmentId ?? 0));

            if (month.HasValue)
                query = query.Where(i => i.BillingMonth == month.Value);
            if (year.HasValue)
                query = query.Where(i => i.BillingYear == year.Value);

            var invoices = await query.ToListAsync();

            var byApartment = invoices
                .GroupBy(i => new { i.ApartmentId, ApartmentCode = i.Apartment?.ApartmentCode })
                .Select(g => new ApartmentPaymentStatDto
                {
                    ApartmentId = g.Key.ApartmentId,
                    ApartmentCode = g.Key.ApartmentCode,
                    TotalInvoices = g.Count(),
                    PaidInvoices = g.Count(i => i.Status == InvoiceStatus.Paid),
                    TotalBilled = g.Sum(i => i.TotalMoney ?? 0),
                    TotalPaid = g.Sum(i => i.Pay ?? 0),
                    TotalDebt = g.Sum(i => i.Debt ?? 0)
                })
                .OrderBy(a => a.ApartmentCode)
                .ToList();

            return new PaymentStatisticsDto
            {
                TotalInvoices = invoices.Count,
                PaidInvoices = invoices.Count(i => i.Status == InvoiceStatus.Paid),
                UnpaidInvoices = invoices.Count(i => i.Status == InvoiceStatus.Unpaid),
                PendingVerificationInvoices = invoices.Count(i => i.Status == InvoiceStatus.PendingVerification),
                TotalBilled = invoices.Sum(i => i.TotalMoney ?? 0),
                TotalRevenue = invoices.Sum(i => i.Pay ?? 0),
                TotalDebt = invoices.Sum(i => i.Debt ?? 0),
                ByApartment = byApartment
            };
        }
    }
}