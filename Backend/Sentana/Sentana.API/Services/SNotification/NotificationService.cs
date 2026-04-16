using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Notification;
using Sentana.API.Models;
using System.Security.Claims;

namespace Sentana.API.Services.SNotification
{
    public class NotificationService : INotificationService
    {
        private readonly SentanaContext _context;

        public NotificationService(SentanaContext context)
        {
            _context = context;
        }

		// Đổi kiểu trả về của hàm thành Tuple chứa Danh sách và Tổng số chưa đọc
		public async Task<(List<NotificationDto> Items, int UnreadCount)> GetMyNotificationsAsync(ClaimsPrincipal user, int pageIndex = 1, int pageSize = 10)
		{
			var accountIdClaim = user.FindFirst("AccountId")?.Value;
			if (!int.TryParse(accountIdClaim, out var accountId))
				throw new UnauthorizedAccessException("Phiên làm việc hết hạn.");

			// 1. Đếm TỔNG SỐ thông báo chưa đọc của user này (Bất kể ở trang nào)
			var totalUnread = await _context.Notifications
				.CountAsync(n => n.AccountId == accountId && !n.IsRead);

			// 2. Phân trang lấy dữ liệu
			var listFromDb = await _context.Notifications
				.Where(n => n.AccountId == accountId)
				.OrderByDescending(n => n.CreatedAt)
				.Skip((pageIndex - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var items = listFromDb.Select(n => new NotificationDto
			{
				NotificationId = n.NotificationId,
				Title = n.Title ?? "Thông báo hệ thống",
				Message = n.Message ?? "",
				IsRead = n.IsRead,
				CreatedAt = n.CreatedAt.ToString("HH:mm dd/MM/yyyy")
			}).ToList();

			return (items, totalUnread);
		}

		public async Task<(bool IsSuccess, string Message)> MarkAsReadAsync(int notificationId, ClaimsPrincipal user)
        {
            var accountIdClaim = user.FindFirst("AccountId")?.Value;
            if (!int.TryParse(accountIdClaim, out var accountId))
                return (false, "Xác thực không hợp lệ.");

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.AccountId == accountId);

            if (notification == null)
                return (false, "Thông báo không tồn tại.");

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return (true, "Thành công.");
        }
    }
}