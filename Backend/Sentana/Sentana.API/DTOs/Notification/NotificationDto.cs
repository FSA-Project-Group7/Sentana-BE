namespace Sentana.API.DTOs.Notification
{
    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        // Format sẵn ngày giờ để Frontend không phải xử lý lại (Tuân thủ DRY)
        public string CreatedAt { get; set; } = string.Empty;
    }
}