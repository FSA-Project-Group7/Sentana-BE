using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Apartment;
using Sentana.API.Services.SApartment;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
	[ApiController]
	[Authorize] 
	public class ApartmentsController : ControllerBase
	{
		private readonly IApartmentService _apartmentService;

		public ApartmentsController(IApartmentService apartmentService)
		{
			_apartmentService = apartmentService;
		}

		[HttpGet]
		public async Task<IActionResult> GetApartmentList()
		{
			var result = await _apartmentService.GetApartmentListAsync();
			return Ok(result);
		}

		[HttpPost]
		[Authorize(Roles = "Manager")] // Phân quyền
		public async Task<IActionResult> CreateApartment([FromBody] CreateApartmentDto newApartmentDto)
		{
			try
			{
				var result = await _apartmentService.CreateApartmentAsync(newApartmentDto);
				return CreatedAtAction(nameof(GetApartmentList), new { id = result.ApartmentId }, new { message = "Tạo phòng mới thành công!", data = result });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}

		[HttpPut("{id}")]
		[Authorize(Roles = "Manager, Admin")] // Phân quyền
		public async Task<IActionResult> UpdateApartment(int id, [FromBody] UpdateApartmentDto updatedDataDto)
		{
			try
			{
				var success = await _apartmentService.UpdateApartmentAsync(id, updatedDataDto);
				if (!success) return NotFound(new { message = "Không tìm thấy phòng này." });

				return Ok(new { message = "Cập nhật thông tin phòng thành công!" });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}

		[HttpPatch("{id}/status")]
		[Authorize(Roles = "Manager, Admin")] // Phân quyền
		public async Task<IActionResult> UpdateStatus(int id, [FromBody] byte newStatus)
		{
			try
			{
				var success = await _apartmentService.UpdateStatusAsync(id, newStatus);
				if (!success) return NotFound(new { message = "Không tìm thấy phòng này." });

				return Ok(new { message = "Cập nhật trạng thái thành công!" });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}

		[HttpDelete("{id}")]
		[Authorize(Roles = "Manager, Admin")] // Phân quyền
		public async Task<IActionResult> DeleteApartment(int id)
		{
			try
			{
				var success = await _apartmentService.DeleteApartmentAsync(id);
				if (!success) return NotFound(new { message = "Không tìm thấy phòng này." });

				return Ok(new { message = "Đã xóa phòng khỏi danh sách thành công!" });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}
		[HttpGet("deleted")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> GetDeletedApartments()
		{
			try { return Ok(await _apartmentService.GetDeletedApartmentsAsync()); }
			catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
		}

		[HttpPut("{id}/restore")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> RestoreApartment(int id)
		{
			try { await _apartmentService.RestoreApartmentAsync(id); return Ok(new { message = "Khôi phục thành công" }); }
			catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
		}

		[HttpDelete("{id}/hard")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> HardDeleteApartment(int id)
		{
			try { await _apartmentService.HardDeleteApartmentAsync(id); return Ok(new { message = "Xóa vĩnh viễn thành công" }); }
			catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
		}
	}
}