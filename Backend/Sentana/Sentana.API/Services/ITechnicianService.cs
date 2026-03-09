using Sentana.API.DTOs.Technician;

namespace Sentana.API.Services
{
    public interface ITechnicianService
    {
        Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician();

        Task<TechnicianResponseDto> CreateTechnician(CreateTechnicianRequestDto technicianRequest, int managerId);

        Task<TechnicianResponseDto> UpdateTechnician(int technicianId, UpdateTechnicianRequestDto technicianRequest, int managerId);

        Task<string> ToggleTechnicianStatus(int technicianId);
    }
}
