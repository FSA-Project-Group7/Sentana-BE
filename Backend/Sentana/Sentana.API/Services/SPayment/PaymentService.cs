using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Payment;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services.SStorage;
using System.Security.Claims;

namespace Sentana.API.Services.SPayment;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IMinioService _minioService;
    private readonly SentanaContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IMinioService minioService,
        SentanaContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _paymentRepository = paymentRepository;
        _minioService = minioService;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    // by ThanhNT EXISTING CODE (GIỮ NGUYÊN) <33-82>
    public async Task<ApiResponse<object>> UploadPaymentProofAsync(int invoiceId, UploadPaymentProofDto request)
    {
        if (invoiceId <= 0)
            return ApiResponse<object>.Fail(400, "Invoice ID không hợp lệ.");

        if (request == null || request.File == null || request.File.Length == 0)
            return ApiResponse<object>.Fail(400, "File không hợp lệ.");

        var invoice = await _paymentRepository.GetInvoiceAsync(invoiceId);
        if (invoice == null)
            return ApiResponse<object>.Fail(404, "Invoice không tồn tại.");

        var userClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("AccountId");
        if (userClaim == null)
            return ApiResponse<object>.Fail(401, "Token không hợp lệ.");

        int userId = int.Parse(userClaim.Value);

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c =>
                c.ContractId == invoice.ContractId &&
                c.IsDeleted == false);

        if (contract == null)
            return ApiResponse<object>.Fail(404, "Contract không tồn tại.");

        if (contract.AccountId != userId)
            return ApiResponse<object>.Fail(403, "Bạn không có quyền upload cho hóa đơn này.");

        var fileUrl = await _minioService.UploadContractAsync(request.File, 0, 0);

        var transaction = new PaymentTransaction
        {
            InvoiceId = invoiceId,
            PaymentProofImage = fileUrl,
            SubmitDate = DateTime.Now,
            Status = 0,
            Note = request.Note,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddPaymentTransactionAsync(transaction);
        await _paymentRepository.SaveAsync();

        return ApiResponse<object>.Success(new
        {
            transactionId = transaction.TransactionId
        }, "Upload thành công.");
    }
    //By ThanhNT - Upload Proof Auto START<84-151>
    public async Task<ApiResponse<object>> UploadPaymentProofAutoAsync(UploadPaymentProofDto request)
    {
        if (request == null || request.File == null || request.File.Length == 0)
            return ApiResponse<object>.Fail(400, "File không hợp lệ.");

        if (request.File.Length > 5 * 1024 * 1024)
            return ApiResponse<object>.Fail(400, "File không được vượt quá 5MB.");

        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(request.File.ContentType))
            return ApiResponse<object>.Fail(400, "Chỉ cho phép JPG/PNG.");

        var userClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("AccountId");
        if (userClaim == null)
            return ApiResponse<object>.Fail(401, "Token không hợp lệ.");

        int userId = int.Parse(userClaim.Value);

        var contract = await _context.Contracts
            .Where(c => c.AccountId == userId
                        && c.IsDeleted == false
                        && c.Status == GeneralStatus.Active)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (contract == null)
            return ApiResponse<object>.Fail(404, "Không có contract active.");

        var invoice = await _context.Invoices
            .Where(i => i.ContractId == contract.ContractId
                        && i.Status == InvoiceStatus.Unpaid
                        && (i.IsDeleted == false || i.IsDeleted == null))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        if (invoice == null)
            return ApiResponse<object>.Fail(404, "Bạn hiện không có hóa đơn nào chưa thanh toán.");

        var existed = await _context.PaymentTransactions
            .AnyAsync(t => t.InvoiceId == invoice.InvoiceId && t.Status == 0);

        if (existed)
            return ApiResponse<object>.Fail(400, "Invoice đã có proof chờ duyệt.");

        var fileUrl = await _minioService.UploadContractAsync(request.File, 0, 0);

        var transaction = new PaymentTransaction
        {
            InvoiceId = invoice.InvoiceId,
            PaymentProofImage = fileUrl,
            SubmitDate = DateTime.Now,
            Status = 0,
            Note = request.Note,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddPaymentTransactionAsync(transaction);
        await _paymentRepository.SaveAsync();

        return ApiResponse<object>.Success(new
        {
            transactionId = transaction.TransactionId,
            invoiceId = invoice.InvoiceId,
            proofUrl = fileUrl,
            status = "Pending"
        }, "Upload thành công (auto).");
    }
    // ThanhNT - Upload Proof Auto END <155-175>
    //  OTHER METHODS

    public async Task<ApiResponse<object>> GetPaymentsByInvoiceAsync(int invoiceId)
    {
        var payments = await _paymentRepository.GetPaymentsByInvoiceAsync(invoiceId);
        return ApiResponse<object>.Success(payments, "OK");
    }

    public async Task<ApiResponse<object>> GetPaymentDetailAsync(int transactionId)
    {
        var transaction = await _paymentRepository.GetTransactionAsync(transactionId);
        return ApiResponse<object>.Success(transaction, "OK");
    }

    public async Task<ApiResponse<object>> GetPaymentHistoryAsync(ClaimsPrincipal user, int? apartmentId = null, int? accountId = null)
    {
        return ApiResponse<object>.Success(new List<object>(), "OK");
    }

    public async Task<ApiResponse<object>> GetAllTransactionsAsync()
    {
        var data = await _context.PaymentTransactions.ToListAsync();
        return ApiResponse<object>.Success(data, "OK");
    }
}