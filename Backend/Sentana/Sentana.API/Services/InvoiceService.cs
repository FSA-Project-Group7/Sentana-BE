using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Invoice;
using Sentana.API.Models;
using Sentana.API.Enums; // ĐÃ GỌI FILE ENUMS CỦA CHÚNG TA VÀO ĐÂY

namespace Sentana.API.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly SentanaContext _context;

        public InvoiceService(SentanaContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // view monthly invoice
        public async Task<InvoiceResponseDto?> GetCurrentInvoiceAsync(ClaimsPrincipal user, int? apartmentId = null, int? accountId = null)
        {
            if (user == null)
                throw new UnauthorizedAccessException("User is null.");

            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                throw new UnauthorizedAccessException("Invalid token or AccountId claim missing.");

            var role = user.FindFirst(ClaimTypes.Role)?.Value
                       ?? user.FindFirst("role")?.Value
                       ?? string.Empty;
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

            int? targetApartmentId = null;

            if (isManager)
            {
                if (apartmentId.HasValue)
                {
                    targetApartmentId = apartmentId.Value;
                }
                else if (accountId.HasValue)
                {
                    var contract = await _context.Contracts
                        .Where(c => c.AccountId == accountId.Value && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (contract == null || contract.ApartmentId == null) return null;
                    targetApartmentId = contract.ApartmentId;
                }
                else
                {
                    var ownContract = await _context.Contracts
                        .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (ownContract != null && ownContract.ApartmentId != null)
                        targetApartmentId = ownContract.ApartmentId;
                }
            }
            else
            {
                var contract = await _context.Contracts
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (contract == null || contract.ApartmentId == null) return null;
                targetApartmentId = contract.ApartmentId;
            }

            if (!targetApartmentId.HasValue) return null;

            var now = DateTime.Now;
            var invoice = await _context.Invoices
                .Include(i => i.Apartment)
                .Include(i => i.Contract)
                .Where(i => i.ApartmentId == targetApartmentId.Value
                            && i.BillingMonth == now.Month
                            && i.BillingYear == now.Year
                            && (i.IsDeleted == false || i.IsDeleted == null))
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync();

            if (invoice == null) return null;

            return new InvoiceResponseDto
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
                Payments = invoice.Payments
            };
        }

        // generate monthly invoices 
        public async Task<(bool IsSuccess, string Message, int GeneratedCount)> GenerateMonthlyInvoicesAsync(GenerateInvoiceRequestDto request, int currentUserId)
        {
            var query = _context.Apartments.Where(a => a.Status == ApartmentStatus.Occupied && a.IsDeleted == false);

            if (request.ApartmentId.HasValue)
            {
                query = query.Where(a => a.ApartmentId == request.ApartmentId.Value);
            }

            var activeApartments = await query.ToListAsync();
            if (!activeApartments.Any()) return (false, "Không có căn hộ nào đủ điều kiện để tạo hóa đơn.", 0);

            int generatedCount = 0;
            int skippedCount = 0;

            foreach (var apt in activeApartments)
            {
                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.ApartmentId == apt.ApartmentId && i.BillingMonth == request.Month && i.BillingYear == request.Year && i.IsDeleted == false);

                if (existingInvoice != null)
                {
                    skippedCount++;
                    continue;
                }

                // Lấy Hợp đồng bằng GeneralStatus.Active
                var contract = await _context.Contracts
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
            }

            if (generatedCount > 0)
            {
                await _context.SaveChangesAsync();
                return (true, $"Tạo thành công {generatedCount} hóa đơn. Bỏ qua {skippedCount} phòng do đã có hóa đơn.", generatedCount);
            }

            return (false, $"Không tạo được hóa đơn nào. Có {skippedCount} phòng đã tồn tại hóa đơn.", 0);
        }
    }
}