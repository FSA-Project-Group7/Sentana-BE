using System.Security.Claims;
using Sentana.API.DTOs.Invoice;

namespace Sentana.API.Services
{
    public interface IInvoiceService
    {
        Task<InvoiceResponseDto?> GetCurrentInvoiceAsync(ClaimsPrincipal user, int? apartmentId = null, int? accountId = null);

        Task<(bool IsSuccess, string Message, int GeneratedCount)> GenerateMonthlyInvoicesAsync(GenerateInvoiceRequestDto request, int currentUserId);
    }
}