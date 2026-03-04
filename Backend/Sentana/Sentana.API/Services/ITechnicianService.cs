using Sentana.API.DTOs.Technician;

namespace Sentana.API.Services
{
    public interface ITechnicianService
    {
        Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician();
    }
}
