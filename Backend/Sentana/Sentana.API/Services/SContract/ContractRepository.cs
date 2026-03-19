using Microsoft.EntityFrameworkCore;
using Sentana.API.Enums;
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
            if (contractId <= 0)
                return null;

            return await _context.Contracts
                .Include(c => c.Apartment)
                .FirstOrDefaultAsync(c =>
                    c.ContractId == contractId &&
                    c.IsDeleted == false);
        }

        public async Task<Apartment?> GetApartmentAsync(int apartmentId)
        {
            if (apartmentId <= 0)
                return null;

            return await _context.Apartments
                .FirstOrDefaultAsync(a =>
                    a.ApartmentId == apartmentId &&
                    a.IsDeleted == false);
        }

        public async Task<Account?> GetAccountAsync(int accountId)
        {
            if (accountId <= 0)
                return null;

            return await _context.Accounts
                .Include(a => a.Role) // 🔥 FIX QUAN TRỌNG
                .FirstOrDefaultAsync(a =>
                    a.AccountId == accountId &&
                    a.IsDeleted == false);
        }

        public async Task<bool> HasActiveContractAsync(int apartmentId)
        {
            if (apartmentId <= 0)
                return false;

            return await _context.Contracts.AnyAsync(c =>
                c.ApartmentId == apartmentId &&
                c.Status == GeneralStatus.Active &&
                c.IsDeleted == false);
        }

        public async Task AddContractAsync(Contract contract)
        {
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));

            await _context.Contracts.AddAsync(contract);
        }

        public async Task AddContractVersionAsync(ContractVersion version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            await _context.ContractVersions.AddAsync(version);
        }

        public async Task<ContractVersion?> GetLatestContractVersionAsync(int contractId)
        {
            if (contractId <= 0)
                return null;

            return await _context.ContractVersions
                .Where(v =>
                    v.ContractId == contractId &&
                    v.IsDeleted == false)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();
        }

        public async Task<Contract?> GetContractDetailAsync(int contractId)
        {
            if (contractId <= 0)
                return null;

            return await _context.Contracts
                .Include(c => c.Apartment)
                .Include(c => c.Account)
                    .ThenInclude(a => a!.Info)
                .Include(c => c.CurrentVersion)
                .FirstOrDefaultAsync(c =>
                    c.ContractId == contractId &&
                    c.IsDeleted == false);
        }

        public async Task<List<Contract>> GetContractListAsync()
        {
            return await _context.Contracts
                .Include(c => c.Apartment)
                .Include(c => c.Account)
                    .ThenInclude(a => a!.Info)
                .Include(c => c.CurrentVersion)
                .Where(c => c.IsDeleted == false)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
        public async Task<Contract?> GetContractByAccountIdAsync(int accountId)
        {
            return await _context.Contracts
                .Include(c => c.Apartment)
                .Include(c => c.Account)
                .Include(c => c.CurrentVersion)
                .FirstOrDefaultAsync(c =>
                    c.AccountId == accountId &&
                    c.Status == GeneralStatus.Active &&
                    c.IsDeleted == false);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}