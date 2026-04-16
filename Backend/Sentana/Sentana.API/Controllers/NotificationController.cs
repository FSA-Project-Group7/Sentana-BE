using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.Services.SNotification;

namespace Sentana.API.Controllers
{
    [ApiController]
    [Route("api/notifications")] // Ép đường dẫn chuẩn
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;

        public NotificationController(INotificationService service) => _service = service;

		[HttpGet("my-notifications")]
		public async Task<IActionResult> GetMyNotifications([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
		{
			try
			{
				// Nhận tham số phân trang từ Query URL
				var result = await _service.GetMyNotificationsAsync(User, pageIndex, pageSize);

				// Trả về cả Items và UnreadCount
				return Ok(new { success = true, items = result.Items, unreadCount = result.UnreadCount });
			}
			catch (Exception ex)
			{
				return BadRequest(new { success = false, message = ex.Message });
			}
		}

		[HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var result = await _service.MarkAsReadAsync(id, User);
                if (!result.IsSuccess) return NotFound(new { message = result.Message });
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}