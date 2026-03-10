using Sentana.API.DTOs.Resident;

namespace Sentana.API.Services
{
    public interface IResidentService
    {
        Task<bool> AssignResident(AssignResidentRequestDto request);
        Task<ResidentResponseDto> CreateResident(CreateResidentRequestDto request, int managerId);

        Task<IEnumerable<ResidentResponseDto>> GetAllResidents();
    }
}
