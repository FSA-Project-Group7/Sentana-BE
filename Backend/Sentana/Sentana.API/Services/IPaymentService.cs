using System.Security.Claims;
using Sentana.API.DTOs.Invoice;
using Sentana.API.DTOs.Payment;
using Sentana.API.Helpers;

namespace Sentana.API.Services
{
    public interface IPaymentService
    {
        // US13 - View Payment History
        Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(
            ClaimsPrincipal user,
            int? apartmentId = null,
            int? accountId = null
        );

        // US15 - Upload Payment Proof
        Task<ApiResponse<object>> UploadPaymentProofAsync(
            int invoiceId,
            UploadPaymentProofDto request
        );
    }
}