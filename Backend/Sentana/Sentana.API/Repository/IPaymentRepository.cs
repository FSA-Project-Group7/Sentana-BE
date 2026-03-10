using Sentana.API.Models;

namespace Sentana.API.Repositories
{
    public interface IPaymentRepository
    {
        Task<Invoice?> GetInvoiceAsync(int invoiceId);

        Task AddPaymentTransactionAsync(PaymentTransaction transaction);

        Task SaveAsync();
    }
}