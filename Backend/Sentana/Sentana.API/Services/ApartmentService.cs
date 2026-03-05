using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Apartment;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services
{
	public class ApartmentService : IApartmentService
	{
		private readonly SentanaContext _context;

		public ApartmentService(SentanaContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<ApartmentDto>> GetApartmentListAsync()
		{
			// Map từ Entity Apartment sang ApartmentDto
			return await _context.Apartments
				.Where(a => a.IsDeleted == false)
				.Select(a => new ApartmentDto
				{
					ApartmentId = a.ApartmentId,
					ApartmentCode = a.ApartmentCode,
					ApartmentName = a.ApartmentName,
					FloorNumber = a.FloorNumber,
					Area = a.Area,
					Status = (byte?)a.Status
				})
				.ToListAsync();
		}

		public async Task<ApartmentDto> CreateApartmentAsync(CreateApartmentDto dto)
		{
			var newApartment = new Apartment
			{
				BuildingId = dto.BuildingId,
				ApartmentCode = dto.ApartmentCode,
				ApartmentName = dto.ApartmentName,
				ApartmentNumber = dto.ApartmentNumber,
				FloorNumber = dto.FloorNumber,
				Area = dto.Area,
				Status = (ApartmentStatus?)dto.Status,
				CreatedAt = DateTime.Now,
				IsDeleted = false
			};

			_context.Apartments.Add(newApartment);
			await _context.SaveChangesAsync();

			return new ApartmentDto
			{
				ApartmentId = newApartment.ApartmentId,
				ApartmentCode = newApartment.ApartmentCode,
				ApartmentName = newApartment.ApartmentName,
				FloorNumber = newApartment.FloorNumber,
				Area = newApartment.Area,
				Status = (byte)newApartment.Status
			};
		}

		public async Task<bool> UpdateApartmentAsync(int id, UpdateApartmentDto dto)
		{
			var apartment = await _context.Apartments
				.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);

			if (apartment == null) return false;

			// Map dữ liệu từ DTO sang Entity hiện tại
			apartment.ApartmentName = dto.ApartmentName;
			apartment.ApartmentCode = dto.ApartmentCode;
			apartment.Area = dto.Area;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> UpdateStatusAsync(int id, byte newStatus)
		{
			var apartment = await _context.Apartments
				.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);

			if (apartment == null) return false;

			apartment.Status = (ApartmentStatus)newStatus;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> DeleteApartmentAsync(int id)
		{
			var apartment = await _context.Apartments
				.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);

			if (apartment == null) return false;

			apartment.IsDeleted = true;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return true;
		}
	}
}