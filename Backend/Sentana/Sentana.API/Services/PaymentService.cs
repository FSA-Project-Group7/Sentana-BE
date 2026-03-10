using Sentana.API.DTOs.Payment;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;

namespace Sentana.API.Services;

public class PaymentService(IPaymentRepository paymentRepository) : IPaymentService
{
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

        var invoice = await paymentRepository.GetInvoiceAsync(invoiceId);

        if (invoice == null)
        {
            return ApiResponse<object>.Fail(404, "Invoice không tồn tại.");
        }

        var fileName = Guid.NewGuid() + Path.GetExtension(request.File.FileName);

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "payment-proofs");

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var filePath = Path.Combine(folder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }

        var transaction = new PaymentTransaction
        {
            InvoiceId = invoiceId,
            PaymentProofImage = "/uploads/payment-proofs/" + fileName,
            SubmitDate = DateTime.Now,
            Status = 0,
            Note = request.Note,
            CreatedAt = DateTime.Now
        };

        await paymentRepository.AddPaymentTransactionAsync(transaction);

        await paymentRepository.SaveAsync();

        return ApiResponse<object>.Success(new
        {
            transactionId = transaction.TransactionId,
            invoiceId = invoiceId,
            proofUrl = transaction.PaymentProofImage,
            status = "Pending"
        }, "Upload payment proof thành công.");
    }
}