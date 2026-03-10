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

    public async Task SaveAsync()
    {
        await context.SaveChangesAsync();
    }
}