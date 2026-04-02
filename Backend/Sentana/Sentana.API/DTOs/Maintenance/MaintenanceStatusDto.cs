namespace Sentana.API.DTOs.Maintenance
{
    // US19 - Track Maintenance Status (Resident)
    public class MaintenanceStatusDto
    {
        public int RequestId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public string? ApartmentCode { get; set; }
        public string? PriorityName { get; set; }
        public string? Status { get; set; }
        public string? AssignedTechnicianName { get; set; }
        public DateTime? CreateDay { get; set; }
        public DateTime? FixDay { get; set; }
        public string? ResolutionNote { get; set; }
        public string? ImageUrl { get; set; }
    }
}
