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
    private readonly SentanaContext _context; // Đã thêm Context vào đây

    public PaymentService(
        IPaymentRepository paymentRepository,
        IMinioService minioService,
        SentanaContext context)
    {
        _paymentRepository = paymentRepository;
        _minioService = minioService;
        _context = context;
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

        // Giữ nguyên Upload lên MinIO
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

    public async Task<ApiResponse<object>> GetPaymentsByInvoiceAsync(int invoiceId)
    {
        if (invoiceId <= 0)
        {
            return ApiResponse<object>.Fail(400, "Invoice ID không hợp lệ.");
        }

        var payments = await _paymentRepository.GetPaymentsByInvoiceAsync(invoiceId);

        return ApiResponse<object>.Success(payments, "Lấy danh sách payment thành công.");
    }

    public async Task<ApiResponse<object>> GetPaymentDetailAsync(int transactionId)
    {
        if (transactionId <= 0)
        {
            return ApiResponse<object>.Fail(400, "Transaction ID không hợp lệ.");
        }

        var transaction = await _paymentRepository.GetTransactionAsync(transactionId);

        if (transaction == null)
        {
            return ApiResponse<object>.Fail(404, "Transaction không tồn tại.");
        }

        return ApiResponse<object>.Success(transaction, "Lấy chi tiết payment thành công.");
    }

    // US13 - View Payment History
    public async Task<ApiResponse<object>> GetPaymentHistoryAsync(ClaimsPrincipal user, int? apartmentId = null, int? accountId = null)
    {
        if (user == null)
        {
            return ApiResponse<object>.Fail(401, "User is null.");
        }

        var accountIdClaim = user.FindFirst("AccountId")?.Value;
        if (!int.TryParse(accountIdClaim, out var callerAccountId))
        {
            return ApiResponse<object>.Fail(401, "Token không hợp lệ hoặc thiếu AccountId.");
        }

        var role = user.FindFirst(ClaimTypes.Role)?.Value
                   ?? user.FindFirst("role")?.Value
                   ?? string.Empty;
        var isManager = string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);

        var targetApartmentId = await ResolveTargetApartmentIdAsync(
            callerAccountId,
            isManager,
            apartmentId,
            accountId);

        if (!targetApartmentId.HasValue)
        {
            return ApiResponse<object>.Success(new List<PaymentHistoryItemDto>(), "Không tìm thấy căn hộ để lấy lịch sử thanh toán.");
        }

        // Đã sửa 'context' thành '_context'
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Apartment)
            .Include(i => i.PaymentTransactions)
            .Where(i => i.ApartmentId == targetApartmentId.Value
                        && i.Status == InvoiceStatus.Paid
                        && (i.IsDeleted == false || i.IsDeleted == null))
            .ToListAsync();

        var history = invoices
            .Select(i =>
            {
                var approvedTransactions = i.PaymentTransactions
                    .Where(t => (t.IsDeleted == false || t.IsDeleted == null) &&
                                t.Status == PaymentTransactionStatus.Approved)
                    .ToList();

                var amountPaid = approvedTransactions.Sum(t => t.AmountPaid ?? 0m);
                if (amountPaid <= 0m)
                {
                    amountPaid = i.Pay ?? 0m;
                }

                DateTime? paidDateTime = null;
                if (i.DayPay.HasValue)
                {
                    paidDateTime = i.DayPay.Value.ToDateTime(TimeOnly.MinValue);
                }
                else if (approvedTransactions.Count > 0)
                {
                    paidDateTime = approvedTransactions
                        .OrderByDescending(t => t.SubmitDate)
                        .Select(t => t.SubmitDate)
                        .FirstOrDefault();
                }

                return new PaymentHistoryItemDto
                {
                    InvoiceId = i.InvoiceId,
                    ApartmentId = i.ApartmentId,
                    ApartmentCode = i.Apartment?.ApartmentCode,
                    BillingMonth = i.BillingMonth,
                    BillingYear = i.BillingYear,
                    TotalMoney = i.TotalMoney,
                    AmountPaid = amountPaid,
                    PaidDate = paidDateTime?.ToString("yyyy-MM-dd")
                };
            })
            .OrderBy(x => x.PaidDate)
            .ThenBy(x => x.InvoiceId)
            .ToList();

        return ApiResponse<object>.Success(history, "Lấy lịch sử thanh toán thành công.");
    }

    private async Task<int?> ResolveTargetApartmentIdAsync(
        int callerAccountId,
        bool isManager,
        int? apartmentId,
        int? accountId)
    {
        if (isManager)
        {
            if (apartmentId.HasValue) return apartmentId.Value;

            if (accountId.HasValue)
            {
                // Đã sửa 'context' thành '_context'
                var contract = await _context.Contracts
                    .AsNoTracking()
                    .Where(c => c.AccountId == accountId.Value &&
                                c.Status == GeneralStatus.Active &&
                                c.IsDeleted == false)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                return contract?.ApartmentId;
            }
        }

        // Đã sửa 'context' thành '_context'
        var residentContract = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.AccountId == callerAccountId &&
                        c.Status == GeneralStatus.Active &&
                        c.IsDeleted == false)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        return residentContract?.ApartmentId;
    }

	public async Task<ApiResponse<object>> GetAllTransactionsAsync()
	{
		var transactions = await _context.PaymentTransactions
			.Include(t => t.Invoice)
				.ThenInclude(i => i.Apartment)
			.Where(t => t.IsDeleted == false)
			.OrderByDescending(t => t.SubmitDate)
			.Select(t => new {
				transactionId = t.TransactionId,
				invoiceId = t.InvoiceId,
				apartmentCode = t.Invoice.Apartment != null ? t.Invoice.Apartment.ApartmentCode : "N/A",
				billingMonth = t.Invoice != null ? t.Invoice.BillingMonth : 0,
				billingYear = t.Invoice != null ? t.Invoice.BillingYear : 0,
				amountPaid = t.AmountPaid,
				submitDate = t.SubmitDate,
				proofUrl = t.PaymentProofImage,
				status = (int)t.Status, // 0: Pending, 1: Approved, 2: Rejected
				note = t.Note
			})
			.ToListAsync();

		return ApiResponse<object>.Success(transactions, "Lấy danh sách giao dịch thành công.");
	}
}