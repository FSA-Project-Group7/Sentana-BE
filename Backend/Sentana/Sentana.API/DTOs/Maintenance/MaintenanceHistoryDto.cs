using Sentana.API.Enums;

namespace Sentana.API.DTOs.Maintenance
{
    // US18 - View Maintenance History (Resident)
    public class MaintenanceHistoryDto
    {
        public int RequestId { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ResolutionNote { get; set; }
        public byte? Priority { get; set; }
        public string? PriorityName { get; set; }
        public DateTime? CreateDay { get; set; }
        public DateTime? FixDay { get; set; }
        public string? AssignedToName { get; set; }
        public string? Status { get; set; }
        public string? ImageUrl { get; set; }
    }
}
