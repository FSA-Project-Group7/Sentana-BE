using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Technician;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class TechnicianService : ITechnicianService
    {
        private readonly SentanaContext _context;

        public TechnicianService(SentanaContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician()
        {
            var technicians = await _context.Accounts.Include(a => a.Info).Where(a => a.RoleId == 3).ToListAsync();
            return technicians.Select(a => new TechnicianResponseDto
            {
                AccountId = a.AccountId,
                UserName = a.UserName,
                FullName = a.Info?.FullName,
                Email = a.Email,
                PhoneNumber = a.Info?.PhoneNumber,
                Status = a.Status,
                TechAvailability = a.TechAvailability
            });
        }
    }
}
