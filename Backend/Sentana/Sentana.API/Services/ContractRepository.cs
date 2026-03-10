using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;

namespace Sentana.API.Repositories
{
    public class ContractRepository : IContractRepository
    {
        private readonly SentanaContext _context;

        public ContractRepository(SentanaContext context)
        {
            _context = context;
        }

        public async Task<Contract?> GetContractWithApartmentAsync(int contractId)
        {
            return await _context.Contracts
                .Include(c => c.Apartment)
                .FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == false);
        }

        public async Task<Apartment?> GetApartmentAsync(int apartmentId)
        {
            return await _context.Apartments
                .FirstOrDefaultAsync(a => a.ApartmentId == apartmentId && a.IsDeleted == false);
        }

        public async Task<Account?> GetAccountAsync(int accountId)
        {
            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.IsDeleted == false);
        }

        public async Task<bool> HasActiveContractAsync(int apartmentId)
        {
            return await _context.Contracts.AnyAsync(c =>
                c.ApartmentId == apartmentId &&
                c.Status == Enums.GeneralStatus.Active &&
                c.IsDeleted == false);
        }

        public async Task AddContractAsync(Contract contract)
        {
            await _context.Contracts.AddAsync(contract);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        // Implementation for the missing method
        public async Task<Contract?> GetContractDetailAsync(int contractId)
        {
            return await _context.Contracts
                .Include(c => c.Apartment)
                .Include(c => c.Account)
                .FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == false);
        }
    }
}