namespace Sentana.API.DTOs.Notification
{
    public class NotificationMessageDto
    {
        public Guid MessageId { get; set; } = Guid.NewGuid();
        public int AccountId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

