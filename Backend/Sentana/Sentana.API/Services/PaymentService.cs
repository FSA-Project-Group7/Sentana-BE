using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Invoice;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly SentanaContext _context;

        public PaymentService(SentanaContext context)
        {
            _context = context;
        }

        public async Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(
            ClaimsPrincipal user,
            int? apartmentId = null,
            int? accountId = null)
        {
            if (user == null) throw new UnauthorizedAccessException("User is null.");

            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var callerAccountId))
                throw new UnauthorizedAccessException("Invalid token or AccountId claim missing.");

            var role = user.FindFirst(ClaimTypes.Role)?.Value
                       ?? user.FindFirst("role")?.Value
                       ?? string.Empty;
            var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

            var targetApartmentId = await ResolveTargetApartmentIdAsync(
                callerAccountId,
                isManager,
                apartmentId,
                accountId);

            if (!targetApartmentId.HasValue) return new List<PaymentHistoryItemDto>();

            var invoices = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Apartment)
                .Include(i => i.PaymentTransactions)
                .Where(i => i.ApartmentId == targetApartmentId.Value
                            && i.Status == InvoiceStatus.Paid
                            && (i.IsDeleted == false || i.IsDeleted == null))
                .ToListAsync();

            var history = invoices
                .Select(i =>
                {
                    var approvedTransactions = i.PaymentTransactions
                        .Where(t => (t.IsDeleted == false || t.IsDeleted == null) && t.Status == PaymentTransactionStatus.Approved)
                        .ToList();

                    var amountPaid = approvedTransactions.Sum(t => t.AmountPaid ?? 0m);
                    if (amountPaid <= 0m) amountPaid = i.Pay ?? 0m;

                    DateTime? paidDateTime = null;
                    if (i.DayPay.HasValue)
                    {
                        paidDateTime = i.DayPay.Value.ToDateTime(TimeOnly.MinValue);
                    }
                    else if (approvedTransactions.Count > 0)
                    {
                        paidDateTime = approvedTransactions
                            .OrderByDescending(t => t.SubmitDate)
                            .Select(t => t.SubmitDate)
                            .FirstOrDefault();
                    }

                    return new PaymentHistoryItemDto
                    {
                        InvoiceId = i.InvoiceId,
                        ApartmentId = i.ApartmentId,
                        ApartmentCode = i.Apartment?.ApartmentCode,
                        BillingMonth = i.BillingMonth,
                        BillingYear = i.BillingYear,
                        TotalMoney = i.TotalMoney,
                        AmountPaid = amountPaid,
                        PaidDate = paidDateTime?.ToString("yyyy-MM-dd")
                    };
                })
                .OrderBy(x => x.PaidDate)
                .ThenBy(x => x.InvoiceId)
                .ToList();

            return history;
        }

        private async Task<int?> ResolveTargetApartmentIdAsync(
            int callerAccountId,
            bool isManager,
            int? apartmentId,
            int? accountId)
        {
            if (isManager)
            {
                if (apartmentId.HasValue) return apartmentId.Value;

                if (accountId.HasValue)
                {
                    var contract = await _context.Contracts
                        .AsNoTracking()
                        .Where(c => c.AccountId == accountId.Value && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefaultAsync();

                    return contract?.ApartmentId;
                }

                var ownContract = await _context.Contracts
                    .AsNoTracking()
                    .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                return ownContract?.ApartmentId;
            }

            var residentContract = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.AccountId == callerAccountId && c.Status == GeneralStatus.Active && c.IsDeleted == false)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            return residentContract?.ApartmentId;
        }
    }
}

