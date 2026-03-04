using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;
using Sentana.API.Enums;

namespace Sentana.API.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class ApartmentsController : ControllerBase
	{
		private readonly SentanaContext _context;

		public ApartmentsController(SentanaContext context)
		{
			_context = context;
		}
		// US32 - Create Room
		[HttpPost]
		public async Task<IActionResult> CreateApartment([FromBody] Apartment newApartment)
		{
			// Set mặc định các giá trị hệ thống
			newApartment.CreatedAt = DateTime.Now;
			newApartment.IsDeleted = false;

			_context.Apartments.Add(newApartment);
			await _context.SaveChangesAsync();

			return CreatedAtAction(nameof(GetApartmentList), new { id = newApartment.ApartmentId }, newApartment);
		}

		// US33 - Update Room 
		[HttpPut("{id}")]
		public async Task<IActionResult> UpdateApartment(int id, [FromBody] Apartment updatedData)
		{
			var apartment = await _context.Apartments
				.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);

			if (apartment == null) return NotFound("Không tìm thấy phòng này.");

			apartment.ApartmentName = updatedData.ApartmentName;
			apartment.ApartmentCode = updatedData.ApartmentCode;
			apartment.Area = updatedData.Area;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return Ok(new { message = "Cập nhật thông tin phòng thành công!" });
		}
		// US34 - Delete Room (Xóa mềm)
		[HttpDelete("{id}")]
		public async Task<IActionResult> DeleteApartment(int id)
		{
			var apartment = await _context.Apartments
				.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);

			if (apartment == null) return NotFound("Không tìm thấy phòng này.");

			apartment.IsDeleted = true;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return Ok(new { message = "Đã xóa phòng khỏi danh sách thành công!" });
		}
		// US35 - View Room List
		[HttpGet]
		public async Task<IActionResult> GetApartmentList()
		{
			var apartments = await _context.Apartments
				.Where(a => a.IsDeleted == false)
				.Select(a => new
				{
					a.ApartmentId,
					a.ApartmentCode,
					a.ApartmentName,
					a.FloorNumber,
					a.Area,
					a.Status // 1: Trống, 2: Đang ở, 3: Bảo trì
				})
				.ToListAsync();

			return Ok(apartments);
		}
		// US36 - Update Room Status
		[HttpPatch("{id}/status")]
		public async Task<IActionResult> UpdateStatus(int id, [FromBody] byte newStatus)
		{
			var apartment = await _context.Apartments
				.FirstOrDefaultAsync(a => a.ApartmentId == id && a.IsDeleted == false);

			if (apartment == null) return NotFound("Không tìm thấy phòng này.");

			apartment.Status = (ApartmentStatus)newStatus;
			apartment.UpdatedAt = DateTime.Now;

			await _context.SaveChangesAsync();
			return Ok(new { message = "Cập nhật trạng thái thành công!" });
		}

	}
}