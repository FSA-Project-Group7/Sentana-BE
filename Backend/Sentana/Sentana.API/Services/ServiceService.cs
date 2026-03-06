using Sentana.API.Models;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Service;
using Sentana.API.Enums;

namespace Sentana.API.Services
{
    public class ServiceService : IServiceService
    {
        private readonly SentanaContext _context;

        public ServiceService(SentanaContext context)
        {
            _context = context;
        }

        // US54 - Create Service
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

        // US55 - Update Service
        public async Task<Service> UpdateServiceAsync(UpdateServiceRequestDto request)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(x => x.ServiceId == request.ServiceId);

            if (service == null)
                throw new Exception("Service not found.");

            service.ServiceName = request.ServiceName;
            service.Description = request.Description;
            service.ServiceFee = request.ServiceFee;
            service.Status = (GeneralStatus)request.Status;

            await _context.SaveChangesAsync();

            return service;
        }

        // US56 - Delete Service
        public async Task DeleteServiceAsync(int serviceId)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(x => x.ServiceId == serviceId);

            if (service == null)
                throw new Exception("Service not found.");

            if (service.Status == GeneralStatus.Inactive)
                throw new Exception("Service already inactive.");

            service.Status = GeneralStatus.Inactive;

            await _context.SaveChangesAsync();
        }

        // US57 - View Service List
        public async Task<IEnumerable<ServiceResponseDto>> GetServiceListAsync()
        {
            var services = await _context.Services.ToListAsync();

            return services.Select(s => new ServiceResponseDto
            {
                ServiceId = s.ServiceId,
                ServiceName = s.ServiceName,
                Description = s.Description,
                ServiceFee = s.ServiceFee ?? 0,
                Status = (int)s.Status,
				CreatedAt = s.CreatedAt
			});
        }

        // US58 - Assign Service to Apartment
        public async Task<bool> AssignServiceToRoom(AssignRoomServiceRequestDto request)
        {
            var exist = await _context.ApartmentServices
                .FirstOrDefaultAsync(x =>
                    x.ApartmentId == request.ApartmentId &&
                    x.ServiceId == request.ServiceId &&
                    x.IsDeleted == false);

            if (exist != null)
                return false;

            var roomService = new Models.ApartmentService
            {
                ApartmentId = request.ApartmentId,
                ServiceId = request.ServiceId,
                Status = GeneralStatus.Active,
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };

            _context.ApartmentServices.Add(roomService);
            await _context.SaveChangesAsync();

            return true;
        }

        // US59 - Remove Service from Apartment
        public async Task<bool> RemoveServiceFromRoom(RemoveRoomServiceRequestDto request)
        {
            var roomService = await _context.ApartmentServices
                .FirstOrDefaultAsync(x =>
                    x.ApartmentId == request.ApartmentId &&
                    x.ServiceId == request.ServiceId &&
                    x.IsDeleted == false);

            if (roomService == null)
                return false;

            

            await _context.SaveChangesAsync();
            return true;
        }

        // US60 - Update Room Service Price
        public async Task<bool> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request)
        {
            var roomService = await _context.ApartmentServices
                .FirstOrDefaultAsync(x =>
                    x.ApartmentId == request.ApartmentId &&
                    x.ServiceId == request.ServiceId &&
                    x.IsDeleted == false);

            if (roomService == null)
                return false;

            roomService.ActualPrice = (decimal?)request.ActualPrice;

            await _context.SaveChangesAsync();
            return true;
        }

        // US61 - View Room Service List
        public async Task<IEnumerable<RoomServiceResponseDto>> GetRoomServiceListAsync(int roomId)
        {
            var roomServices = await _context.ApartmentServices
                .Where(x => x.ApartmentId == roomId && x.IsDeleted == false)
                .Include(x => x.Service)
                .ToListAsync();

            return roomServices.Select(rs => new RoomServiceResponseDto
            {
                ApartmentId = rs.ApartmentId ?? 0,
                ServiceId = rs.ServiceId ?? 0,
                ServiceName = rs.Service.ServiceName,
                ActualPrice = rs.ActualPrice
            });
        }
    }
}