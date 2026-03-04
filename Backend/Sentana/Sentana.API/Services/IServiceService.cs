using Sentana.API.DTOs.Service;
using Sentana.API.Models;

namespace ApartmentBuildingManagement.API.Services
{
    public interface IServiceService
    {
        Task DeleteServiceAsync(int serviceId);
        Task<Service> CreateServiceAsync(CreateServiceRequestDto request);
    }
}