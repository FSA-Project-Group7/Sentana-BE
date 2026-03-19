using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Service;
using Sentana.API.Enums;
using Sentana.API.Models;
using System.Security.Claims;

namespace Sentana.API.Services.SService
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
                Status = GeneralStatus.Active,
                IsDeleted = false
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            return service;
        }

        public async Task<Service> UpdateServiceAsync(UpdateServiceRequestDto request)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(x => x.ServiceId == request.ServiceId && x.IsDeleted == false);

            if (service == null)
                throw new KeyNotFoundException("Không tìm thấy dịch vụ.");

            service.ServiceName = request.ServiceName;
            service.Description = request.Description;
            service.ServiceFee = request.ServiceFee;
            service.Status = request.Status;

            await _context.SaveChangesAsync();

            return service;
        }

        public async Task DeleteServiceAsync(int serviceId)
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.IsDeleted == false);

            if (service == null)
                throw new KeyNotFoundException("Không tìm thấy dịch vụ.");

            service.Status = GeneralStatus.Inactive;
            service.IsDeleted = true;

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<ServiceResponseDto>> GetServiceListAsync()
        {
            var services = await _context.Services
                .Where(x => x.IsDeleted == false)
                .ToListAsync();

            if (!services.Any())
                throw new KeyNotFoundException("Dịch vụ không tồn tại");

            return services.Select(s => new ServiceResponseDto
            {
                ServiceId = s.ServiceId,
                ServiceName = s.ServiceName ?? "",
                Description = s.Description ?? "",
                ServiceFee = s.ServiceFee ?? 0,
                Status = (int)(s.Status ?? GeneralStatus.Inactive),
                CreatedAt = s.CreatedAt
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

            var roomService = new ApartmentService
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

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<RoomServiceResponseDto>> GetRoomServiceListAsync(int roomId)
        {
            var roomServices = await _context.ApartmentServices
                .Where(x => x.ApartmentId == roomId && x.IsDeleted == false)
                .Include(x => x.Service)
                .ToListAsync();

            if (!roomServices.Any())
                throw new KeyNotFoundException("Phòng chưa có dịch vụ nào.");

            return roomServices.Select(rs => new RoomServiceResponseDto
            {
                ApartmentId = rs.ApartmentId ?? 0,
                ServiceId = rs.ServiceId ?? 0,
                ServiceName = rs.Service?.ServiceName ?? "",
                ActualPrice = rs.ActualPrice ?? 0
            });
        }

        public async Task<bool> ApartmentExistsAsync(int apartmentId)
        {
            return await _context.Apartments
                .AnyAsync(x => x.ApartmentId == apartmentId && x.IsDeleted == false);
        }

        public async Task<bool> ServiceExistsAsync(int serviceId)
        {
            return await _context.Services
                .AnyAsync(x => x.ServiceId == serviceId && x.IsDeleted == false);
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

        public async Task<bool> IsResidentRoomAsync(int accountId, int roomId)
        {
            var contract = await _context.Contracts
                .FirstOrDefaultAsync(x =>
                    x.AccountId == accountId &&
                    x.Status == GeneralStatus.Active &&
                    x.IsDeleted == false);

            if (contract == null)
                return false;

            return contract.ApartmentId == roomId;
        }
    }
}