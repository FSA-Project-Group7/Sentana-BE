using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Notification;
using Sentana.API.Models;

namespace Sentana.API.Services.SNotification
{
    public class NotificationService : INotificationService
    {
        private readonly SentanaContext _context;

        public NotificationService(SentanaContext context)
        {
            _context = context;
        }

        public async Task<List<NotificationDto>> GetMyNotificationsAsync(ClaimsPrincipal user)
        {
            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var accountId))
                throw new UnauthorizedAccessException("Token không hợp lệ.");

            var notifications = await _context.Notifications
                .Where(n => n.AccountId == accountId)
                .OrderBy(n => n.IsRead)
                .ThenByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt.ToString("HH:mm dd/MM/yyyy")
                })
                .ToListAsync();

            return notifications;
        }

        public async Task<(bool IsSuccess, string Message)> MarkAsReadAsync(int notificationId, ClaimsPrincipal user)
        {
            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var accountId))
                return (false, "Token không hợp lệ.");

            // Tìm thông báo và Validate quyền sở hữu (Authorization Check)
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.AccountId == accountId);

            if (notification == null)
                return (false, "Không tìm thấy thông báo hoặc bạn không có quyền truy cập.");

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return (true, "Đã đánh dấu đọc.");
        }
    }
}