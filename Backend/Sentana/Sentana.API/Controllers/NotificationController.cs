using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Notification;
using Sentana.API.Helpers;
using Sentana.API.Services.SNotification;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Yêu cầu bảo mật: Phải có Token mới được gọi (Authentication required)
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Endpoint 1: Lấy danh sách
        [HttpGet("my-notifications")]
        public async Task<IActionResult> GetMyNotifications()
        {
            try
            {
                var dtos = await _notificationService.GetMyNotificationsAsync(User);
                return Ok(ApiResponse<List<NotificationDto>>.Success(dtos, "Lấy danh sách thông báo thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<string>.Fail(401, ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail(400, ex.Message));
            }
        }

        // Endpoint 2: Đánh dấu đã đọc
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var result = await _notificationService.MarkAsReadAsync(id, User);
                if (result.IsSuccess)
                    return Ok(ApiResponse<string>.Success(null, result.Message));

                return BadRequest(ApiResponse<string>.Fail(400, result.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail(400, ex.Message));
            }
        }
    }
}   