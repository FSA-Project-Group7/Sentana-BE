using Sentana.API.DTOs.Resident;

namespace Sentana.API.Services.SResident
{
    public interface IResidentService
    {
        Task<ResidentResponseDto> CreateResident(CreateResidentRequestDto request, int managerId);

        Task<IEnumerable<ResidentResponseDto>> GetAllResidents();

        Task<ResidentResponseDto> UpdateResident(int residentId, UpdateResidentRequestDto request, int managerId);

        Task<ImportResidentsResultDto> ImportResidentsFromExcelAsync(Stream fileStream, int managerId);

        Task<(bool IsSuccess, string Message)> AssignResidentToRoomAsync(AssignResidentRequestDto request, int managerId);

        // now returns detailed DTO (no model changes required)
        Task<RemoveResidentResponseDto> RemoveResidentFromRoomAsync(RemoveResidentRequestDto request, int managerId);

        Task<string> ToggleResidentStatus(int residentId);

        Task<bool> DeleteResident(int residentId, int managerId);

        Task<IEnumerable<ResidentResponseDto>> GetDeletedResidents();

        Task<bool> RestoreResident(int residentId, int managerId);

        Task<bool> HardDeleteResident(int residentId);
        Task<MyRoomResponseDto?> GetMyRoomAsync(int accountId);
    }
}
