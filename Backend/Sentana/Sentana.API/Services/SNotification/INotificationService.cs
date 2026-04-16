using System.Security.Claims;
using Sentana.API.DTOs.Notification;

namespace Sentana.API.Services.SNotification
{
    public interface INotificationService
    {
		// Lấy danh sách thông báo của user đang đăng nhập
		Task<(List<NotificationDto> Items, int UnreadCount)> GetMyNotificationsAsync(ClaimsPrincipal user, int pageIndex = 1, int pageSize = 10);

		// Đánh dấu 1 thông báo là đã đọc
		Task<(bool IsSuccess, string Message)> MarkAsReadAsync(int notificationId, ClaimsPrincipal user);
    }
}