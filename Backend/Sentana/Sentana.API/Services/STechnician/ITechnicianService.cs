using Sentana.API.DTOs.Technician;

namespace Sentana.API.Services.STechnician
{
    public interface ITechnicianService
    {
        Task<IEnumerable<TechnicianResponseDto>> GetAllTechnician();

        Task<TechnicianResponseDto> CreateTechnician(CreateTechnicianRequestDto technicianRequest, int managerId);

        Task<TechnicianResponseDto> UpdateTechnician(int technicianId, UpdateTechnicianRequestDto technicianRequest, int managerId);

        Task<string> ToggleTechnicianStatus(int technicianId);
		Task<string> ToggleTechAvailability(int technicianId);

		Task<bool> DeleteTechnician(int technicianId, int managerId);

        Task<IEnumerable<TechnicianResponseDto>> GetDeletedTechnicians();

        Task<bool> RestoreTechnician(int technicianId, int managerId);

        Task<bool> HardDeleteTechnician(int technicianId);

        Task<IEnumerable<TechnicianAvailableDto>> GetAvailableTechnicians();
		Task<TechnicianResponseDto> GetTechnicianProfileById(int accountId);
	}
}
