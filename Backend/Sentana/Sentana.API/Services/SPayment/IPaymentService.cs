using Sentana.API.DTOs.Payment;
using Sentana.API.Helpers;

namespace Sentana.API.Services.SPayment
{
    public interface IPaymentService
    {
        Task<ApiResponse<object>> UploadPaymentProofAsync(int invoiceId, UploadPaymentProofDto request);
        Task<ApiResponse<object>> GetPaymentsByInvoiceAsync(int invoiceId);
        Task<ApiResponse<object>> GetPaymentDetailAsync(int transactionId);
    }
}