using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Invoice;
using Sentana.API.Models;
using Sentana.API.Enums;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Payment;
using Sentana.API.Services.SEmail;
using Sentana.API.Helpers;

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

        // view monthly invoice
        public async Task<List<InvoiceResponseDto>> GetCurrentInvoicesAsync(ClaimsPrincipal user, int? month = null, int? year = null, int? apartmentId = null, int? accountId = null)
        {
            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                throw new UnauthorizedAccessException("Token không hợp lệ.");

            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

            var targetApartmentIds = new List<int>();

            // xác định danh sách phòng cần xem
            if (isManager)
            {
                if (apartmentId.HasValue) targetApartmentIds.Add(apartmentId.Value);
                else if (accountId.HasValue)
                {
                    // manager truyền vào account id
                    var aptIds = await _context.Contracts
                        .Where(c => c.AccountId == accountId.Value && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                        .Select(c => c.ApartmentId)
                        .Where(id => id.HasValue).Select(id => id!.Value).ToListAsync();
                    targetApartmentIds.AddRange(aptIds);
                }
            }
            else // nếu là resident thì tự ra invoice luôn
            {
                var aptIds = await _context.Contracts
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .Select(c => c.ApartmentId)
                    .Where(id => id.HasValue).Select(id => id!.Value).ToListAsync();
                targetApartmentIds.AddRange(aptIds);
            }

            if (!targetApartmentIds.Any()) return new List<InvoiceResponseDto>();

            // tạo hóa đơn chi tiết
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

            // nếu không truyền thời gian thì lấy hóa đơn mới nhất
            var invoicesToProcess = month.HasValue && year.HasValue
                ? rawInvoices
                : rawInvoices.GroupBy(i => i.ApartmentId).Select(g => g.First()).ToList();

            var result = new List<InvoiceResponseDto>();

            // ráp hóa đơn
            foreach (var invoice in invoicesToProcess)
            {
                var dto = new InvoiceResponseDto
                {
                    InvoiceId = invoice.InvoiceId,
                    ApartmentId = invoice.ApartmentId,
                    ApartmentCode = invoice.Apartment?.ApartmentCode,
                    ContractId = invoice.ContractId,
                    BillingMonth = invoice.BillingMonth,
                    BillingYear = invoice.BillingYear,
                    TotalMoney = invoice.TotalMoney, 
                    ServiceFee = invoice.ServiceFee,
                    Pay = invoice.Pay,
                    Debt = invoice.Debt,             
                    WaterNumber = invoice.WaterNumber,
                    ElectricNumber = invoice.ElectricNumber,
                    DayCreat = invoice.DayCreat?.ToString("yyyy-MM-dd"),
                    DayPay = invoice.DayPay?.ToString("yyyy-MM-dd"),
                    StatusName = invoice.Status?.ToString() ?? string.Empty,
                    Payments = invoice.Payments,
                    Details = new List<InvoiceDetailItemDto>()
                };

                if (invoice.Contract != null)
                    dto.Details.Add(new InvoiceDetailItemDto { FeeName = "Tiền thuê phòng", Amount = invoice.Contract.MonthlyRent ?? 0 });

                if (invoice.ElectricMeter != null)
                {
                    var usage = (invoice.ElectricMeter.NewIndex ?? 0) - (invoice.ElectricMeter.OldIndex ?? 0);
                    var price = invoice.ElectricMeter.Price ?? 0;
                    dto.Details.Add(new InvoiceDetailItemDto { FeeName = $"Tiền điện ({usage} kWh)", Amount = usage * price });
                }

                if (invoice.WaterMeter != null)
                {
                    var usage = (invoice.WaterMeter.NewIndex ?? 0) - (invoice.WaterMeter.OldIndex ?? 0);
                    var price = invoice.WaterMeter.Price ?? 0;
                    dto.Details.Add(new InvoiceDetailItemDto { FeeName = $"Tiền nước ({usage} khối)", Amount = usage * price });
                }

                var services = await _context.ApartmentServices
                    .Include(s => s.Service)
                    .Where(s => s.ApartmentId == invoice.ApartmentId && s.Status == GeneralStatus.Active && s.IsDeleted == false)
                    .ToListAsync();

                decimal currentDynamicServiceTotal = services.Sum(s => s.ActualPrice ?? 0m);
                decimal historicalServiceFee = invoice.ServiceFee ?? 0m;

                if (currentDynamicServiceTotal == historicalServiceFee)
                {
                    foreach (var svc in services)
                    {
                        dto.Details.Add(new InvoiceDetailItemDto
                        {
                            FeeName = svc.Service?.ServiceName ?? "Phí dịch vụ",
                            Amount = svc.ActualPrice ?? 0
                        });
                    }
                }
                else
                {
                    if (historicalServiceFee > 0)
                    {
                        dto.Details.Add(new InvoiceDetailItemDto
                        {
                            FeeName = "Phí dịch vụ (Đã chốt theo kỳ hóa đơn)",
                            Amount = historicalServiceFee
                        });
                    }
                }

                result.Add(dto);
            }

            return result;
        }
        // generate monthly invoices 
        public async Task<(bool IsSuccess, string Message, int GeneratedCount)> GenerateMonthlyInvoicesAsync(GenerateInvoiceRequestDto request, int currentUserId)
        {
            // validate time 
            var validation = ValidationHelper.ValidateMonthYear(request.Month, request.Year);
            if (!validation.IsValid)
            {
                return (false, validation.ErrorMessage, 0);
            }

            var query = _context.Apartments.Where(a => a.Status == ApartmentStatus.Occupied && a.IsDeleted == false);

            if (request.ApartmentId.HasValue)
            {
                query = query.Where(a => a.ApartmentId == request.ApartmentId.Value);
            }

            var activeApartments = await query.ToListAsync();
            if (!activeApartments.Any()) return (false, "Không có căn hộ nào đủ điều kiện để tạo hóa đơn.", 0);

            int generatedCount = 0;
            int skippedCount = 0;

            // BỔ SUNG: Khởi tạo danh sách chờ để gửi Email đồng thời (Parallel Execution)
            var emailTasks = new List<Task>();

            foreach (var apt in activeApartments)
            {
                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.ApartmentId == apt.ApartmentId && i.BillingMonth == request.Month && i.BillingYear == request.Year && i.IsDeleted == false);

                if (existingInvoice != null)
                {
                    skippedCount++;
                    continue;
                }

                var contract = await _context.Contracts
                    .Include(c => c.Account)
                    .Where(c => c.ApartmentId == apt.ApartmentId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                decimal rentAmount = contract?.MonthlyRent ?? 0m;

                var elecMeter = await _context.ElectricMeters
                    .FirstOrDefaultAsync(e => e.ApartmentId == apt.ApartmentId && e.RegistrationDate.HasValue && e.RegistrationDate.Value.Month == request.Month && e.RegistrationDate.Value.Year == request.Year && e.IsDeleted == false);

                decimal elecConsumption = (elecMeter?.NewIndex ?? 0) - (elecMeter?.OldIndex ?? 0);
                decimal elecMoney = elecConsumption * (elecMeter?.Price ?? 3500m);

                var waterMeter = await _context.WaterMeters
                    .FirstOrDefaultAsync(w => w.ApartmentId == apt.ApartmentId && w.RegistrationDate.HasValue && w.RegistrationDate.Value.Month == request.Month && w.RegistrationDate.Value.Year == request.Year && w.IsDeleted == false);

                decimal waterConsumption = (waterMeter?.NewIndex ?? 0) - (waterMeter?.OldIndex ?? 0);
                decimal waterMoney = waterConsumption * (waterMeter?.Price ?? 25000m);

                // Lấy Dịch vụ bằng GeneralStatus.Active
                var services = await _context.ApartmentServices
                    .Where(s => s.ApartmentId == apt.ApartmentId && s.Status == GeneralStatus.Active && s.IsDeleted == false)
                    .ToListAsync();
                decimal totalServiceFee = services.Sum(s => s.ActualPrice ?? 0m);

                decimal totalAmount = rentAmount + elecMoney + waterMoney + totalServiceFee;

                var invoice = new Invoice
                {
                    ApartmentId = apt.ApartmentId,
                    ContractId = contract?.ContractId,
                    ElectricMeterId = elecMeter?.ElectricMeterId,
                    WaterMeterId = waterMeter?.WaterMeterId,
                    BillingMonth = request.Month,
                    BillingYear = request.Year,
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

                // lưu thông báo và gửi mail
                if (contract != null && contract.AccountId.HasValue)
                {
                    // Tạo bản ghi Thông báo (Notification Record) lưu xuống Database
                    var notification = new Notification
                    {
                        AccountId = contract.AccountId.Value,
                        Title = "Hóa đơn dịch vụ mới",
                        Message = $"Ban quản lý đã xuất hóa đơn tháng {request.Month}/{request.Year} cho căn hộ {apt.ApartmentCode}. Vui lòng kiểm tra và thanh toán.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notification);

                    // Chuẩn bị tác vụ gửi Email (Email Task) nạp vào hàng chờ
                    if (contract.Account != null && !string.IsNullOrEmpty(contract.Account.Email))
                    {
                        string residentName = contract.Account.UserName ?? "Quý khách";
                        string emailSubject = $"[SENTANA] Thông báo cước phí tháng {request.Month}/{request.Year}";
                        string emailBody = $@"
                            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                                <h2 style='color: #00c292;'>THÔNG BÁO CƯỚC PHÍ DỊCH VỤ</h2>
                                <p>Kính gửi {residentName} (Căn hộ {apt.ApartmentCode}),</p>
                                <p>Hóa đơn dịch vụ tháng <strong>{request.Month}/{request.Year}</strong> của bạn đã được khởi tạo.</p>
                                <p>Tổng số tiền cần thanh toán: <strong style='color:#dc3545; font-size: 1.2em;'>{totalAmount:N0} VNĐ</strong>.</p>
                                <p>Vui lòng đăng nhập vào ứng dụng Sentana để xem chi tiết.</p>
                                <br/>
                                <p>Trân trọng,<br/>Ban Quản Lý Tòa Nhà Sentana.</p>
                            </div>";

                        emailTasks.Add(_emailService.SendEmailAsync(contract.Account.Email, emailSubject, emailBody));
                    }
                }
            }

            if (generatedCount > 0)
            {
                // Lưu giao dịch cơ sở dữ liệu (Database Transaction) - Cả Hóa đơn và Thông báo sẽ được lưu cùng lúc
                await _context.SaveChangesAsync();

                // Kích hoạt gửi Email hàng loạt chạy ngầm (Fire-and-forget Parallel Execution)
                if (emailTasks.Any())
                {
                    _ = Task.WhenAll(emailTasks);
                }

                return (true, $"Tạo thành công {generatedCount} hóa đơn. Bỏ qua {skippedCount} phòng do đã có hóa đơn.", generatedCount);
            }

            return (false, $"Không tạo được hóa đơn nào. Có {skippedCount} phòng đã tồn tại hóa đơn.", 0);
        }

        // view invoice list ( phân trang + lọc )
        public async Task<PagedResult<InvoiceListItemDto>> GetInvoiceListAsync(InvoiceListRequestDto request)
        {
            var query = _context.Invoices
                .Include(i => i.Apartment)
                .Where(i => i.IsDeleted == false);

            // filter
            if (request.Month.HasValue)
                query = query.Where(i => i.BillingMonth == request.Month.Value);

            if (request.Year.HasValue)
                query = query.Where(i => i.BillingYear == request.Year.Value);

            if (request.Status.HasValue)
                query = query.Where(i => i.Status == request.Status.Value);

            // đếm tổng số bản ghi thỏa mãn
            int totalCount = await query.CountAsync();

            // phân trang và Sắp xếp (Mới nhất lên đầu)
            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            // map sang DTO
            var items = invoices.Select(i => new InvoiceListItemDto
            {
                InvoiceId = i.InvoiceId,
                ApartmentId = i.ApartmentId,
                ApartmentCode = i.Apartment?.ApartmentCode,
                BillingMonth = i.BillingMonth,
                BillingYear = i.BillingYear,
                TotalMoney = i.TotalMoney,
                Debt = i.Debt,
                StatusName = i.Status?.ToString() ?? string.Empty,
                CreatedAt = i.CreatedAt?.ToString("yyyy-MM-dd HH:mm")
            }).ToList();

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

            if (invoice == null) return (false, "Không tìm thấy hóa đơn.");
            if (invoice.Status != Enums.InvoiceStatus.Unpaid) return (false, "Chỉ được chỉnh sửa khi hóa đơn chưa thanh toán.");

            if (request.AdditionalFee.HasValue && request.AdditionalFee.Value > 0)
            {
                invoice.TotalMoney += request.AdditionalFee.Value;
                invoice.Debt += request.AdditionalFee.Value;
                // Có thể lưu thêm note vào DB nếu bảng Invoice của bạn có cột Note/Description
            }

            _context.Invoices.Update(invoice);
            bool isSaved = await _context.SaveChangesAsync() > 0;

            return isSaved ? (true, "Cập nhật hóa đơn thành công.") : (false, "Lỗi khi cập nhật.");
        }

        // gửi email nhắc nợ hóa đơn
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

            // template mail 
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
                <p>Quý khách vui lòng đăng nhập vào <b>Cổng thông tin Cư dân Sentana</b> để xem chi tiết hóa đơn (Điện, Nước, Phí dịch vụ) và tải lên biên lai chuyển khoản.</p>
                <br/>
                <p>Trân trọng,<br/><strong>Ban Quản Lý Tòa Nhà Sentana</strong></p>
            </div>";

            await _emailService.SendEmailAsync(account.Email, subject, body);

            return (true, "Đã gửi email thông báo nhắc nợ thành công.");
        }

        // accept thanh toán
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

            bool isSaved = await _context.SaveChangesAsync() > 0;

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

        // từ chối thanh toán
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

                // tạo notification
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
    }
}