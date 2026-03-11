using Sentana.API.DTOs.Resident;

namespace Sentana.API.Services.SResident
{
    public interface IResidentService
    {
        Task<bool> AssignResident(AssignResidentRequestDto request);
        Task<ResidentResponseDto> CreateResident(CreateResidentRequestDto request, int managerId);

        Task<IEnumerable<ResidentResponseDto>> GetAllResidents();

        Task<ImportResidentsResultDto> ImportResidentsFromExcelAsync(Stream fileStream, int managerId);
    }
}
