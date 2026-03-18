using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;

namespace Sentana.API.Repositories
{
    public class ServiceRepository : IServiceRepository
    {
        private readonly SentanaContext _context;

        public ServiceRepository(SentanaContext context)
        {
            _context = context;
        }

        public async Task<List<Service>> GetRoomServicesAsync(int apartmentId)
        {
            return await _context.RoomServices
                .Where(r => r.ApartmentId == apartmentId)
                .Select(r => r.Service!)
                .ToListAsync();
        }

        public async Task<Contract?> GetResidentContractAsync(int accountId)
        {
            return await _context.Contracts
                .FirstOrDefaultAsync(c =>
                    c.AccountId == accountId &&
                    c.Status == Enums.GeneralStatus.Active &&
                    c.IsDeleted == false);
        }

        public async Task<bool> ApartmentExistsAsync(int apartmentId)
        {
            return await _context.Apartments
                .AnyAsync(a => a.ApartmentId == apartmentId && a.IsDeleted == false);
        }
    }
}