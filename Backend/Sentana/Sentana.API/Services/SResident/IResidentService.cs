using Sentana.API.DTOs.Resident;

namespace Sentana.API.Services.SResident
{
    public interface IResidentService
    {
        Task<ResidentResponseDto> CreateResident(CreateResidentRequestDto request, int managerId);

        Task<IEnumerable<ResidentResponseDto>> GetAllResidents(int managerId);

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

        // US08 - View Roommates
        Task<List<RoommateDto>> GetMyRoommatesAsync(int accountId);

        // US09 - View Electricity Index
        Task<List<ElectricityIndexDto>> GetMyElectricityIndexAsync(int accountId, int? month, int? year);

        // US10 - View Water Index
        Task<List<WaterIndexDto>> GetMyWaterIndexAsync(int accountId, int? month, int? year);

        // US11 - View Service Fees
        Task<List<ServiceFeeDto>> GetMyServiceFeesAsync(int accountId);
    }
}
