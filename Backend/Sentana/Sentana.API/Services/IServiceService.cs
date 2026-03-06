using Sentana.API.DTOs.Service;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public interface IServiceService
    {
        // US54 - Create Service
        Task<Service> CreateServiceAsync(CreateServiceRequestDto request);

        // US55 - Update Service
        Task<Service> UpdateServiceAsync(UpdateServiceRequestDto request);

        // US56 - Delete Service
        Task DeleteServiceAsync(int serviceId);

        // US57 - View Service List
        Task<IEnumerable<ServiceResponseDto>> GetServiceListAsync();

        // US58 - Assign Service to Room
        Task<bool> AssignServiceToRoom(AssignRoomServiceRequestDto request);

        // US59 - Remove Service from Room
        Task<bool> RemoveServiceFromRoom(RemoveRoomServiceRequestDto request);

        // US60 - Update Room Service Price
        Task<bool> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request);

        // US61 - View Room Service List
        Task<IEnumerable<RoomServiceResponseDto>> GetRoomServiceListAsync(int roomId);
    }
}