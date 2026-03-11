using Sentana.API.DTOs.Service;
using Sentana.API.Models;
using System.Security.Claims;

namespace Sentana.API.Services.SService
{
    public interface IServiceService
    {
        Task<Service> CreateServiceAsync(CreateServiceRequestDto request);
        Task<Service> UpdateServiceAsync(UpdateServiceRequestDto request);
        Task DeleteServiceAsync(int serviceId);
        Task<IEnumerable<ServiceResponseDto>> GetServiceListAsync();
        Task<bool> AssignServiceToRoom(AssignRoomServiceRequestDto request);
        Task<bool> RemoveServiceFromRoom(RemoveRoomServiceRequestDto request);
        Task<bool> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request);
        Task<IEnumerable<RoomServiceResponseDto>> GetRoomServiceListAsync(int roomId);
        Task<bool> ApartmentExistsAsync(int apartmentId);
        Task<bool> ServiceExistsAsync(int serviceId);
        Task<bool> CheckRoomServiceRelationAsync(int apartmentId, int serviceId);
        Task<bool> IsUserAuthorizedToModifyRoomService(ClaimsPrincipal user, int apartmentId);
    }
}