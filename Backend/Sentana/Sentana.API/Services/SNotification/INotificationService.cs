using System.Security.Claims;
using Sentana.API.DTOs.Notification;

namespace Sentana.API.Services.SNotification
{
    public interface INotificationService
    {
        // Lấy danh sách thông báo của user đang đăng nhập
        Task<List<NotificationDto>> GetMyNotificationsAsync(ClaimsPrincipal user);

        // Đánh dấu 1 thông báo là đã đọc
        Task<(bool IsSuccess, string Message)> MarkAsReadAsync(int notificationId, ClaimsPrincipal user);
    }
}