using Sentana.API.DTOs.Technician;

namespace Sentana.API.Services
{
    public interface ITechnicianService
    {
        Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician();

        Task<TechnicianResponseDto> CreateTechnician(CreateTechnicianRequestDto technicianRequest);

        Task<TechnicianResponseDto> UpdateTechnician(int technicianId, UpdateTechnicianRequestDto technicianRequest);

        Task<string> ToggleTechnicianStatus(int technicianId);
    }
}
