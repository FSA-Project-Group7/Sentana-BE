using Sentana.API.Models;

namespace Sentana.API.Repositories
{
    public interface IServiceRepository
    {
        Task<List<Service>> GetRoomServicesAsync(int apartmentId);

        Task<Contract?> GetResidentContractAsync(int accountId);

        Task<bool> ApartmentExistsAsync(int apartmentId);
    }
}