using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs;
using Sentana.API.Services;
using System.Threading.Tasks;

namespace Sentana.API.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class NewsController : ControllerBase
	{
		private readonly INewsService _newsService;

		public NewsController(INewsService newsService)
		{
			_newsService = newsService;
		}

		[HttpGet]
		public async Task<IActionResult> GetActiveNews()
		{
			var data = await _newsService.GetActiveNewsAsync();
			return Ok(new { data = data, message = "Tải dữ liệu thành công" });
		}

		[HttpGet("deleted")]
		public async Task<IActionResult> GetDeletedNews()
		{
			var data = await _newsService.GetDeletedNewsAsync();
			return Ok(new { data = data, message = "Tải dữ liệu đã xóa thành công" });
		}

		[HttpPost]
		public async Task<IActionResult> CreateNews([FromBody] NewsDto dto)
		{
			if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Description))
				return BadRequest(new { message = "Tiêu đề và nội dung không được để trống." });

			var data = await _newsService.CreateNewsAsync(dto);
			return Ok(new { data = data, message = "Đăng bản tin mới thành công!" });
		}

		[HttpPut("{id}")]
		public async Task<IActionResult> UpdateNews(int id, [FromBody] NewsDto dto)
		{
			if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Description))
				return BadRequest(new { message = "Tiêu đề và nội dung không được để trống." });

			var success = await _newsService.UpdateNewsAsync(id, dto);
			if (!success) return NotFound(new { message = "Không tìm thấy bản tin." });

			return Ok(new { message = "Cập nhật bản tin thành công!" });
		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> SoftDeleteNews(int id)
		{
			var success = await _newsService.SoftDeleteNewsAsync(id);
			if (!success) return NotFound(new { message = "Không tìm thấy bản tin để xóa." });

			return Ok(new { message = "Đã đưa bản tin vào danh sách xóa." });
		}

		[HttpPut("{id}/restore")]
		public async Task<IActionResult> RestoreNews(int id)
		{
			var success = await _newsService.RestoreNewsAsync(id);
			if (!success) return NotFound(new { message = "Không tìm thấy bản tin để khôi phục." });

			return Ok(new { message = "Khôi phục bản tin thành công!" });
		}

		[HttpDelete("{id}/hard")]
		public async Task<IActionResult> HardDeleteNews(int id)
		{
			var success = await _newsService.HardDeleteNewsAsync(id);
			if (!success) return NotFound(new { message = "Không tìm thấy bản tin để xóa vĩnh viễn." });

			return Ok(new { message = "Đã xóa vĩnh viễn bản tin!" });
		}
	}
}