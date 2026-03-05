using Sentana.API.DTOs.Apartment;

namespace Sentana.API.Services
{
	public interface IApartmentService
	{
		Task<IEnumerable<ApartmentDto>> GetApartmentListAsync();
		Task<ApartmentDto> CreateApartmentAsync(CreateApartmentDto newApartmentDto);
		Task<bool> UpdateApartmentAsync(int id, UpdateApartmentDto updatedDataDto);
		Task<bool> UpdateStatusAsync(int id, byte newStatus);
		Task<bool> DeleteApartmentAsync(int id);
	}
}