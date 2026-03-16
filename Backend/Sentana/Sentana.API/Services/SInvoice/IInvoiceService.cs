using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Invoice;
using Sentana.API.DTOs.Payment;

namespace Sentana.API.Services.SInvoice
{
    public interface IInvoiceService
    {
        Task<List<InvoiceResponseDto>> GetCurrentInvoicesAsync(ClaimsPrincipal user, int? month = null, int? year = null, int? apartmentId = null, int? accountId = null);
        Task<(bool IsSuccess, string Message, int GeneratedCount)> GenerateMonthlyInvoicesAsync(GenerateInvoiceRequestDto request, int currentUserId);
        Task<PagedResult<InvoiceListItemDto>> GetInvoiceListAsync(InvoiceListRequestDto request);
        Task<(bool IsSuccess, string Message)> EditInvoiceAsync(int invoiceId, EditInvoiceDto request);
        // Manager duyệt thanh toán
        Task<(bool IsSuccess, string Message)> ApprovePaymentAsync(int transactionId, int currentUserId);
        // Manager từ chối thanh toán
        Task<(bool IsSuccess, string Message)> RejectPaymentAsync(int transactionId, RejectPaymentDto request, int currentUserId);
    }
}