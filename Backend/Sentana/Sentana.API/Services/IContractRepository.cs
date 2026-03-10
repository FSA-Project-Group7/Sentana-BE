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
        Task<Contract?> GetContractDetailAsync(int contractId);

        Task SaveAsync(); // dùng để lưu thay đổi vào database.
    }
}