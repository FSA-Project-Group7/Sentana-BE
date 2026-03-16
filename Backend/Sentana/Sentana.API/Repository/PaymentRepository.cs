using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;

namespace Sentana.API.Repositories;

public class PaymentRepository(SentanaContext context) : IPaymentRepository
{
    public async Task<Invoice?> GetInvoiceAsync(int invoiceId)
    {
        return await context.Invoices
            .FirstOrDefaultAsync(i =>
                i.InvoiceId == invoiceId &&
                i.IsDeleted == false);
    }

    public async Task AddPaymentTransactionAsync(PaymentTransaction transaction)
    {
        await context.PaymentTransactions.AddAsync(transaction);
    }
    public async Task<List<PaymentTransaction>> GetPaymentsByInvoiceAsync(int invoiceId)
    {
        return await context.PaymentTransactions
            .Where(x => x.InvoiceId == invoiceId && x.IsDeleted == false)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<PaymentTransaction?> GetTransactionAsync(int transactionId)
    {
        return await context.PaymentTransactions
            .FirstOrDefaultAsync(x =>
                x.TransactionId == transactionId &&
                x.IsDeleted == false);
    }

    public async Task SaveAsync()
    {
        await context.SaveChangesAsync();
    }
}