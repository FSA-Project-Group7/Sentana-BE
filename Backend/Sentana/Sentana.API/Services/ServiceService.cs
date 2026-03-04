using ApartmentBuildingManagement.API.Enums;
using ApartmentBuildingManagement.API.Models;
using Microsoft.EntityFrameworkCore;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace ApartmentBuildingManagement.API.Services
{
    public class ServiceService : IServiceService
    {
        private readonly SentanaContext _context;

        public ServiceService(SentanaContext context)
        {
            _context = context;
        }

        public async Task DeleteServiceAsync(int serviceId)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

            if (service == null)
                throw new Exception("Service not found.");

            if (service.Status == GeneralStatus.Inactive)
                throw new Exception("Service is already inactive.");

            service.Status = GeneralStatus.Inactive;

            await _context.SaveChangesAsync();
        }
    }
}