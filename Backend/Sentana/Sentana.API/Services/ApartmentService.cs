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
				.OrderByDescending(a => a.ApartmentId) 
				.ToListAsync();
		}

		public async Task<ApartmentDto> CreateApartmentAsync(CreateApartmentDto dto)
		{
			var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == dto.BuildingId && b.IsDeleted == false);
			if (building == null) throw new ArgumentException("Tòa nhà này không tồn tại hoặc đã bị xóa.");

			if (dto.FloorNumber > building.FloorNumber)
				throw new ArgumentException($"Tầng của căn hộ ({dto.FloorNumber}) không được vượt quá số tầng của tòa nhà ({building.FloorNumber}).");

			bool isCodeExist = await _context.Apartments.AnyAsync(a => a.ApartmentCode!.ToLower() == dto.ApartmentCode!.ToLower() && a.IsDeleted == false);
			if (isCodeExist) throw new ArgumentException("Mã căn hộ này đã tồn tại trong hệ thống.");

			bool isNameExist = await _context.Apartments.AnyAsync(a => a.BuildingId == dto.BuildingId && a.ApartmentName!.ToLower() == dto.ApartmentName!.ToLower() && a.IsDeleted == false);
			if (isNameExist) throw new ArgumentException("Tên căn hộ này đã tồn tại trong tòa nhà này.");

			bool isAppNumberExist = await _context.Apartments.AnyAsync(a => a.BuildingId == dto.BuildingId && a.ApartmentNumber == dto.ApartmentNumber && a.IsDeleted == false);
			if (isAppNumberExist) throw new ArgumentException("Số căn hộ (Apartment Number) này đã tồn tại trong tòa nhà này.");

			if (!Enum.IsDefined(typeof(ApartmentStatus), (ApartmentStatus)dto.Status))
				throw new ArgumentException("Trạng thái căn hộ không hợp lệ.");

			var newApartment = new Apartment
			{
				BuildingId = dto.BuildingId,
				ApartmentCode = dto.ApartmentCode,
				ApartmentName = dto.ApartmentName,
				ApartmentNumber = dto.ApartmentNumber,
				FloorNumber = dto.FloorNumber,
				Area = dto.Area,
				Status = (ApartmentStatus)dto.Status,
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
			var apartment = await _context.Apartments.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);
			if (apartment == null) return false;

			var building = await _context.Buildings.FirstOrDefaultAsync(b => b.BuildingId == apartment.BuildingId && b.IsDeleted == false);

			if (dto.FloorNumber.HasValue && building != null && dto.FloorNumber.Value > building.FloorNumber)
				throw new ArgumentException($"Tầng của căn hộ ({dto.FloorNumber.Value}) không được vượt quá số tầng của tòa nhà ({building.FloorNumber}).");

			bool isCodeExist = await _context.Apartments.AnyAsync(a => a.ApartmentId != id && a.ApartmentCode!.ToLower() == dto.ApartmentCode!.ToLower() && a.IsDeleted == false);
			if (isCodeExist) throw new ArgumentException("Mã căn hộ này đã được sử dụng cho phòng khác.");

			bool isNameExist = await _context.Apartments.AnyAsync(a => a.ApartmentId != id && a.BuildingId == apartment.BuildingId && a.ApartmentName!.ToLower() == dto.ApartmentName!.ToLower() && a.IsDeleted == false);
			if (isNameExist) throw new ArgumentException("Tên căn hộ này đã tồn tại trong tòa nhà này.");

			if (dto.ApartmentNumber.HasValue)
			{
				bool isAppNumberExist = await _context.Apartments.AnyAsync(a => a.ApartmentId != id && a.BuildingId == apartment.BuildingId && a.ApartmentNumber == dto.ApartmentNumber.Value && a.IsDeleted == false);
				if (isAppNumberExist) throw new ArgumentException("Số căn hộ (Apartment Number) này đã tồn tại trong tòa nhà này.");
				apartment.ApartmentNumber = dto.ApartmentNumber.Value;
			}

			if (dto.Status.HasValue)
			{
				if (!Enum.IsDefined(typeof(ApartmentStatus), (ApartmentStatus)dto.Status.Value))
					throw new ArgumentException("Trạng thái căn hộ không hợp lệ.");
				apartment.Status = (ApartmentStatus)dto.Status.Value;
			}

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