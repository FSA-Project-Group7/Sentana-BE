public async Task<ApiResponse<object>> ReviewPaymentAsync(int transactionId, ReviewPaymentDto request)
{
    if (transactionId <= 0)
    {
        return ApiResponse<object>.Fail(400, "Transaction ID không hợp lệ.");
    }

    if (request == null)
    {
        return ApiResponse<object>.Fail(400, "Request body không hợp lệ.");
    }

    if (request.Status != 1 && request.Status != 2)
    {
        return ApiResponse<object>.Fail(400, "Status không hợp lệ.");
    }

    var transaction = await paymentRepository.GetTransactionAsync(transactionId);

    if (transaction == null)
    {
        return ApiResponse<object>.Fail(404, "Transaction không tồn tại.");
    }

    if (transaction.Status != 0)
    {
        return ApiResponse<object>.Fail(400, "Transaction đã được xử lý.");
    }

    var invoice = await paymentRepository.GetInvoiceByIdAsync(transaction.InvoiceId ?? 0);

    if (invoice == null)
    {
        return ApiResponse<object>.Fail(404, "Invoice không tồn tại.");
    }

    transaction.Status = request.Status;
    transaction.Note = request.Note;

    if (request.Status == 1)
    {
        invoice.Status = 3; // Paid
        invoice.Pay = transaction.AmountPaid;
        invoice.Debt = 0;
    }

    await paymentRepository.SaveAsync();

    return ApiResponse<object>.Success(new
    {
        transactionId = transaction.TransactionId,
        status = transaction.Status
    }, "Review payment thành công.");
}