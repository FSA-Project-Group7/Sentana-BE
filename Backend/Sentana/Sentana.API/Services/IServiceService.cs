using Sentana.API.DTOs.Service;
using Sentana.API.Models;

namespace Sentana.API.Services
{
    public interface IServiceService
    {
        Task DeleteServiceAsync(int serviceId);
        Task<Service> CreateServiceAsync(CreateServiceRequestDto request);
        Task<Service> UpdateServiceAsync(UpdateServiceRequestDto request);

        Task<bool> UpdateRoomServicePrice(UpdateRoomServicePriceRequestDto request);
        Task<bool> RemoveServiceFromRoom(RemoveRoomServiceRequestDto request);

    }
}