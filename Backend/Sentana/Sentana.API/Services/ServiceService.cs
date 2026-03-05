using Sentana.API.Models;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Service;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public class ServiceService : IServiceService
    {
        private readonly SentanaContext _context;

        public ServiceService(SentanaContext context)
        {
            _context = context;
        }

        public async Task<Service> CreateServiceAsync(CreateServiceRequestDto request)
        {
            var service = new Service
            {
                ServiceName = request.ServiceName,
                Description = request.Description,
                ServiceFee = request.ServiceFee,
                Status = GeneralStatus.Active
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            return service;
        }

        public async Task<Service> UpdateServiceAsync(UpdateServiceRequestDto request)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.ServiceId == request.ServiceId);

            if (service == null)
                throw new Exception("Service not found.");

            service.ServiceName = request.ServiceName;
            service.Description = request.Description;
            service.ServiceFee = request.ServiceFee;
            service.Status = (GeneralStatus)request.Status;

            await _context.SaveChangesAsync();

            return service;
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


        public async Task<bool> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request)
        public async Task<bool> AssignServiceToRoom(AssignRoomServiceRequestDto request)
        {
            var exist = await _context.ApartmentServices
                .FirstOrDefaultAsync(x =>
                    x.ApartmentId == request.ApartmentId &&
                    x.ServiceId == request.ServiceId);

            if (exist != null)
                return false;

            var roomService = new Models.ApartmentService
            {
                ApartmentId = request.ApartmentId,
                ServiceId = request.ServiceId
            };

            _context.ApartmentServices.Add(roomService);

            await _context.SaveChangesAsync();

            return true;
        }
        public async Task<bool> RemoveServiceFromRoom(RemoveRoomServiceRequestDto request)
        {
            var roomService = await _context.ApartmentServices
                .FirstOrDefaultAsync(x =>
                    x.ApartmentId == request.ApartmentId &&
                    x.ServiceId == request.ServiceId);

            if (roomService == null)
                return false;


            roomService.ActualPrice = request.ActualPrice;
            _context.ApartmentServices.Remove(roomService);


            await _context.SaveChangesAsync();

            return true;
        }
    }
}