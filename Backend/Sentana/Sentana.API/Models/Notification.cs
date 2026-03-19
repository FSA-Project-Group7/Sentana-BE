using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sentana.API.Models
{
    [Table("Notifications")]
    public class Notification
    {
        [Key] // Khóa chính (Primary Key)
        public int NotificationId { get; set; }

        [Required]
        public int AccountId { get; set; } // Khóa ngoại (Foreign Key) liên kết với bảng Account

        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false; // Cờ đánh dấu trạng thái đọc (Boolean flag)

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Thuộc tính điều hướng (Navigation Property) giúp EF Core hiểu mối quan hệ
        [ForeignKey("AccountId")]
        public virtual Account? Account { get; set; }
    }
}