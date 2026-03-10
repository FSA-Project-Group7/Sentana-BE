using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Sentana.API.DTOs.Invoice;

namespace Sentana.API.Services.SInvoice
{
    public interface IInvoiceService
    {
        Task<List<InvoiceResponseDto>> GetCurrentInvoicesAsync(ClaimsPrincipal user, int? month = null, int? year = null, int? apartmentId = null, int? accountId = null);

        Task<(bool IsSuccess, string Message, int GeneratedCount)> GenerateMonthlyInvoicesAsync(GenerateInvoiceRequestDto request, int currentUserId);
    }
}