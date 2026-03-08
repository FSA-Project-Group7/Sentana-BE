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
				.OrderByDescending(a => a.ApartmentId) // Ưu tiên phòng mới tạo lên đầu
				.ToListAsync();
		}

		public async Task<ApartmentDto> CreateApartmentAsync(CreateApartmentDto dto)
		{
			bool isCodeExist = await _context.Apartments
				.AnyAsync(a => a.ApartmentCode!.ToLower() == dto.ApartmentCode!.ToLower() && a.IsDeleted == false);

			if (isCodeExist)
				throw new ArgumentException("Mã phòng này đã tồn tại trong hệ thống.");

			var newApartment = new Apartment
			{
				BuildingId = dto.BuildingId,
				ApartmentCode = dto.ApartmentCode,
				ApartmentName = dto.ApartmentName,
				ApartmentNumber = dto.ApartmentNumber,
				FloorNumber = dto.FloorNumber,
				Area = dto.Area,
				Status = ApartmentStatus.Vacant,
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

			bool isCodeExist = await _context.Apartments
				.AnyAsync(a => a.ApartmentId != id && a.ApartmentCode!.ToLower() == dto.ApartmentCode!.ToLower() && a.IsDeleted == false);

			if (isCodeExist)
				throw new ArgumentException("Mã phòng này đã được sử dụng cho phòng khác.");

			// Map dữ liệu
			apartment.ApartmentName = dto.ApartmentName;
			apartment.ApartmentCode = dto.ApartmentCode;
			if (dto.Area.HasValue) apartment.Area = dto.Area.Value;
			if (dto.FloorNumber.HasValue) apartment.FloorNumber = dto.FloorNumber.Value;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> UpdateStatusAsync(int id, byte newStatus)
		{
			// Kiểm tra trạng thái gửi lên có hợp lệ không
			if (!Enum.IsDefined(typeof(ApartmentStatus), (ApartmentStatus)newStatus))
				throw new ArgumentException("Trạng thái phòng không hợp lệ.");

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