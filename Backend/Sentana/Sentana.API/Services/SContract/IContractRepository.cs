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

        Task AddContractVersionAsync(ContractVersion version);

        Task<ContractVersion?> GetLatestContractVersionAsync(int contractId);

        Task<Contract?> GetContractDetailAsync(int contractId);

        Task<List<Contract>> GetContractListAsync();
        Task<Contract?> GetContractByAccountIdAsync(int accountId);

        Task SaveAsync();
    }
}