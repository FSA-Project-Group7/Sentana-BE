using Sentana.API.DTOs.Resident;

namespace Sentana.API.Services.SResident
{
    public interface IResidentService
    {
        Task<ResidentResponseDto> CreateResident(CreateResidentRequestDto request, int managerId);

        Task<IEnumerable<ResidentResponseDto>> GetAllResidents();

        Task<ImportResidentsResultDto> ImportResidentsFromExcelAsync(Stream fileStream, int managerId);

        Task<(bool IsSuccess, string Message)> AssignResidentToRoomAsync(AssignResidentRequestDto request, int managerId);
    }
}
