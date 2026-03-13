using Sentana.API.DTOs.Payment;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services.SStorage;

namespace Sentana.API.Services.SPayment;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IMinioService _minioService;


    public PaymentService(
    IPaymentRepository paymentRepository,
    IMinioService minioService)
    {
        _paymentRepository = paymentRepository;
        _minioService = minioService;
    }

    public async Task<ApiResponse<object>> UploadPaymentProofAsync(int invoiceId, UploadPaymentProofDto request)
    {
        if (invoiceId <= 0)
        {
            return ApiResponse<object>.Fail(400, "Invoice ID không hợp lệ.");
        }

        if (request == null)
        {
            return ApiResponse<object>.Fail(400, "Request body không được để trống.");
        }

        if (request.File == null)
        {
            return ApiResponse<object>.Fail(400, "File thanh toán không được để trống.");
        }

        if (request.File.Length == 0)
        {
            return ApiResponse<object>.Fail(400, "File upload rỗng.");
        }

        if (request.File.Length > 5 * 1024 * 1024)
        {
            return ApiResponse<object>.Fail(400, "File không được vượt quá 5MB.");
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };

        if (!allowedTypes.Contains(request.File.ContentType))
        {
            return ApiResponse<object>.Fail(400, "Chỉ cho phép file JPG hoặc PNG.");
        }

        var invoice = await _paymentRepository.GetInvoiceAsync(invoiceId);

        if (invoice == null)
        {
            return ApiResponse<object>.Fail(404, "Invoice không tồn tại.");
        }

        var proofUrl = await _minioService.UploadFileAsync(request.File, "payment-proofs");

        var transaction = new PaymentTransaction
        {
            InvoiceId = invoiceId,
            PaymentProofImage = proofUrl,
            SubmitDate = DateTime.Now,
            Status = 0,
            Note = request.Note,
            CreatedAt = DateTime.Now
        };

        await _paymentRepository.AddPaymentTransactionAsync(transaction);
        await _paymentRepository.SaveAsync();

        return ApiResponse<object>.Success(new
        {
            transactionId = transaction.TransactionId,
            invoiceId,
            proofUrl = transaction.PaymentProofImage,
            status = "Pending"
        }, "Upload payment proof thành công.");
    }


}
