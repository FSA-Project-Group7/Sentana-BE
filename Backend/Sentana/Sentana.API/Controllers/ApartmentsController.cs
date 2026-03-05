using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Apartment;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
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
		public async Task<IActionResult> CreateApartment([FromBody] CreateApartmentDto newApartmentDto)
		{
			var result = await _apartmentService.CreateApartmentAsync(newApartmentDto);
			return CreatedAtAction(nameof(GetApartmentList), new { id = result.ApartmentId }, result);
		}

		[HttpPut("{id}")]
		public async Task<IActionResult> UpdateApartment(int id, [FromBody] UpdateApartmentDto updatedDataDto)
		{
			var success = await _apartmentService.UpdateApartmentAsync(id, updatedDataDto);
			if (!success) return NotFound(new { message = "Không tìm thấy phòng này." });

			return Ok(new { message = "Cập nhật thông tin phòng thành công!" });
		}

		[HttpPatch("{id}/status")]
		public async Task<IActionResult> UpdateStatus(int id, [FromBody] byte newStatus)
		{
			var success = await _apartmentService.UpdateStatusAsync(id, newStatus);
			if (!success) return NotFound(new { message = "Không tìm thấy phòng này." });

			return Ok(new { message = "Cập nhật trạng thái thành công!" });
		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> DeleteApartment(int id)
		{
			var success = await _apartmentService.DeleteApartmentAsync(id);
			if (!success) return NotFound(new { message = "Không tìm thấy phòng này." });

			return Ok(new { message = "Đã xóa phòng khỏi danh sách thành công!" });
		}
	}
}