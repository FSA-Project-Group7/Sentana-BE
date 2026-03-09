using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Service;
using Sentana.API.Enums;
using Sentana.API.Models;
using System.Security.Claims;

namespace Sentana.API.Services
{
    public class ServiceService : IServiceService
    {
        private readonly SentanaContext _context;

        public ServiceService(SentanaContext context) => _context = context;

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
                .FirstOrDefaultAsync(x => x.ServiceId == request.ServiceId)
                ?? throw new Exception("Không tìm thấy dịch vụ.");

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
                .FirstOrDefaultAsync(x => x.ServiceId == serviceId)
                ?? throw new Exception("Không tìm thấy dịch vụ.");

            if (service.Status == GeneralStatus.Inactive)
                throw new Exception("Dịch vụ hiện đã không hoạt động.");

            service.Status = GeneralStatus.Inactive;

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<ServiceResponseDto>> GetServiceListAsync()
        {
            var services = await _context.Services.ToListAsync();

            return services.Select(static s =>
            {
                int status = (int)(s.Status ?? GeneralStatus.Inactive); // Ensure non-null value
                return new ServiceResponseDto
                {
                    ServiceId = s.ServiceId,
                    ServiceName = s.ServiceName ?? string.Empty, // Ensure non-null value
                    Description = s.Description ?? string.Empty, // Ensure non-null value
                    ServiceFee = s.ServiceFee ?? 0,
                    Status = status,
                    CreatedAt = s.CreatedAt
                };
            });
        }

        public async Task<bool> AssignServiceToRoom(AssignRoomServiceRequestDto request)
        {
            var exist = await _context.ApartmentServices
                .AnyAsync(x =>
                    x.ApartmentId == request.ApartmentId &&
                    x.ServiceId == request.ServiceId &&
                    x.IsDeleted == false);

            if (exist)
                return false;

            var roomService = new Models.ApartmentService
            {
                ApartmentId = request.ApartmentId,
                ServiceId = request.ServiceId,
                Status = GeneralStatus.Active,
                StartDay = DateOnly.FromDateTime(DateTime.Now),
                CreatedAt = DateTime.Now,
                IsDeleted = false
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
                    x.ServiceId == request.ServiceId &&
                    x.IsDeleted == false);

            if (roomService == null)
                return false;

            roomService.IsDeleted = true;
            roomService.Status = GeneralStatus.Inactive;
            roomService.EndDay = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request)
        {
            var roomService = await _context.ApartmentServices
                .FirstOrDefaultAsync(x =>
                    x.ApartmentId == request.ApartmentId &&
                    x.ServiceId == request.ServiceId &&
                    x.IsDeleted == false);

            if (roomService == null)
                return false;

            roomService.ActualPrice = request.ActualPrice;
            roomService.EndDay = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();
            return true;
        }

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
                ServiceName = rs.Service?.ServiceName ?? string.Empty,
                ActualPrice = rs.ActualPrice ?? 0
            });
        }
        public async Task<bool> ApartmentExistsAsync(int apartmentId)
        {
            return await _context.Apartments
                .AnyAsync(a => a.ApartmentId == apartmentId);
        }

        public async Task<bool> ServiceExistsAsync(int serviceId)
        {
            return await _context.Services
                .AnyAsync(s => s.ServiceId == serviceId);
        }

        public async Task<bool> CheckRoomServiceRelationAsync(int apartmentId, int serviceId)
        {
            return await _context.ApartmentServices
                .AnyAsync(x =>
                    x.ApartmentId == apartmentId &&
                    x.ServiceId == serviceId &&
                    x.IsDeleted == false);
        }

        public Task<bool> IsUserAuthorizedToModifyRoomService(ClaimsPrincipal user, int apartmentId)
        {
            return Task.FromResult(user?.Identity?.IsAuthenticated ?? false);
        }
    }
}