using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Invoice;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly SentanaContext _context;

        public InvoiceService(SentanaContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<InvoiceResponseDto?> GetCurrentInvoiceAsync(ClaimsPrincipal user, int? apartmentId = null, int? accountId = null)
        {
            if (user == null)
                throw new UnauthorizedAccessException("User is null.");

            // validate token / get caller account id
            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                throw new UnauthorizedAccessException("Invalid token or AccountId claim missing.");

            var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
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
                        .Where(c => c.AccountId == accountId.Value && c.IsDeleted == false)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (contract == null || contract.ApartmentId == null)
                        return null;

                    targetApartmentId = contract.ApartmentId;
                }
                else
                {
                    // allow manager to see their own invoice if no param passed
                    var ownContract = await _context.Contracts
                        .Where(c => c.AccountId == callerAccountId && c.IsDeleted == false)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (ownContract != null && ownContract.ApartmentId != null)
                        targetApartmentId = ownContract.ApartmentId;
                }
            }
            else
            {
                // resident flow
                var contract = await _context.Contracts
                    .Where(c => c.AccountId == callerAccountId && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (contract == null || contract.ApartmentId == null)
                    return null;

                targetApartmentId = contract.ApartmentId;
            }

            if (!targetApartmentId.HasValue)
                return null;

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
                DayCreat = invoice.DayCreat?.ToString(),
                DayPay = invoice.DayPay?.ToString(),
                StatusName = invoice.Status?.ToString() ?? string.Empty,
                Payments = invoice.Payments
            };
        }
    }
}