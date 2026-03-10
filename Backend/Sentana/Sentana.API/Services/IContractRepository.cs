using Sentana.API.Models;

namespace Sentana.API.Repositories
{
    public interface IContractRepository
    {
        Task<Contract?> GetContractWithApartmentAsync(int contractId);

        Task<Apartment?> GetApartmentAsync(int apartmentId);

        Task<Account?> GetAccountAsync(int accountId);

        Task<bool> HasActiveContractAsync(int apartmentId);

        Task AddContractAsync(Contract contract);

        Task SaveAsync();
    }
}