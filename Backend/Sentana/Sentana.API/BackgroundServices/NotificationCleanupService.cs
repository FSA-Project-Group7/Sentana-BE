using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;

namespace Sentana.API.BackgroundServices
{
    public class NotificationCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationCleanupService> _logger;

        // Sử dụng Tiêm phụ thuộc (Dependency Injection) để lấy ScopeFactory và Logger
        public NotificationCleanupService(IServiceScopeFactory scopeFactory, ILogger<NotificationCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // Hàm này sẽ tự động chạy ngầm ở một Luồng (Thread) riêng biệt
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification Cleanup Service Start.");

            // Infinite Loop giữ cho tác vụ sống liên tục trừ khi tắt app
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOldNotificationsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Có lỗi xảy ra trong quá trình dọn dẹp thông báo.");
                }

                // Ngủ đông 24 tiếng (Task.Delay) để không làm nghẽn CPU, sau đó dậy chạy tiếp
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }

        private async Task CleanupOldNotificationsAsync()
        {
            // Tạo một Phạm vi (Scope) mới để lấy DbContext một cách an toàn
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SentanaContext>();

            // Mốc thời gian: Lấy thời điểm 30 ngày trước
            var cutoffDate = DateTime.Now.AddDays(-30);

            // Tìm các thông báo: ĐÃ ĐỌC (IsRead == true) VÀ TẠO QUÁ 30 NGÀY (CreatedAt <= cutoffDate)
            var oldNotifications = await context.Notifications
                .Where(n => n.IsRead == true && n.CreatedAt <= cutoffDate)
                .ToListAsync();

            if (oldNotifications.Any())
            {
                context.Notifications.RemoveRange(oldNotifications);
                await context.SaveChangesAsync();
                
                _logger.LogInformation($"[Cleanup Job] Đã xóa vĩnh viễn {oldNotifications.Count} thông báo cũ rác.");
            }
        }
    }
}