using Sentana.API.Enums;

namespace Sentana.API.DTOs.Maintenance
{
    public class MaintenanceResponseDto
    {
        public int RequestId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public MaintenancePriority Priority { get; set; }
        public MaintenanceRequestStatus Status { get; set; }

        // Thời gian
        public DateTime? CreateDay { get; set; }
        public DateTime? FixDay { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Thông tin Căn hộ & Danh mục
        public int? ApartmentId { get; set; }
        public string ApartmentName { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; }

        // Thông tin Con người (Gồm cả ID để FE xử lý logic và Tên để hiển thị)
        public int? AccountId { get; set; }
        public string ResidentName { get; set; }
        public int? AssignedTo { get; set; }
        public string AssignedTechnicianName { get; set; }

        // Kết quả xử lý
        public string? ImageUrl { get; set; } // Ảnh minh chứng sự cố
        public string ResolutionNote { get; set; }
        public string? FixedImageUrl { get; set; }
    }
}
