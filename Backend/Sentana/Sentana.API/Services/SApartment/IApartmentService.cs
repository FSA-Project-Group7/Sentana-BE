using Sentana.API.DTOs.Apartment;

namespace Sentana.API.Services.SApartment
{
    public interface IApartmentService
    {
        Task<IEnumerable<ApartmentDto>> GetApartmentListAsync(int managerId, int? buildingId = null);
        Task<ApartmentDto> CreateApartmentAsync(CreateApartmentDto newApartmentDto);
        Task<bool> UpdateApartmentAsync(int id, UpdateApartmentDto updatedDataDto);
        Task<bool> UpdateStatusAsync(int id, byte newStatus);
		Task<bool> DeleteApartmentAsync(int id, System.Security.Claims.ClaimsPrincipal user = null);
		Task<IEnumerable<ApartmentResponseDto>> GetDeletedApartmentsAsync();
		Task<bool> RestoreApartmentAsync(int id);
		Task<bool> HardDeleteApartmentAsync(int id);
	}
}