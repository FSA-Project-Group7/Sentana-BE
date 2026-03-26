using Sentana.API.Enums;

namespace Sentana.API.DTOs.Maintenance
{
    public class MaintenanceTaskDto
    {
        public int RequestId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public string? ApartmentCode { get; set; }
        public string Priority { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime? CreateDay { get; set; }
    }
}