using Sentana.API.Models;

namespace Sentana.API.Services
{
    public interface IServiceService
    {
        Task DeleteServiceAsync(int serviceId);
    }
}