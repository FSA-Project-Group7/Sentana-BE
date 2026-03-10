using System.Security.Claims;
using Sentana.API.DTOs.Invoice;

namespace Sentana.API.Services
{
    public interface IPaymentService
    {
        Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(ClaimsPrincipal user, int? apartmentId = null, int? accountId = null);
    }
}

