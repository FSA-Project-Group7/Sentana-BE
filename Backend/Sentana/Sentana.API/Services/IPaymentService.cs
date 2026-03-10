using Sentana.API.DTOs.Payment;
using Sentana.API.Helpers;

namespace Sentana.API.Services
{
    public interface IPaymentService
    {
        Task<ApiResponse<object>> UploadPaymentProofAsync(int invoiceId, UploadPaymentProofDto request);
    }
}