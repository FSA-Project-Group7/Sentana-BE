namespace Sentana.API.DTOs.Building
{
    // US79 - View Occupancy Dashboard (Manager)
    public class OccupancyDashboardDto
    {
        public int TotalApartments { get; set; }
        public int OccupiedApartments { get; set; }
        public int VacantApartments { get; set; }
        public int MaintenanceApartments { get; set; }
        public double OccupancyRate { get; set; }   // Tỉ lệ lấp đầy (%)

        public List<BuildingOccupancyDto> ByBuilding { get; set; } = new();
    }

    public class BuildingOccupancyDto
    {
        public int BuildingId { get; set; }
        public string? BuildingName { get; set; }
        public string? BuildingCode { get; set; }
        public int TotalApartments { get; set; }
        public int OccupiedApartments { get; set; }
        public int VacantApartments { get; set; }
        public int MaintenanceApartments { get; set; }
        public double OccupancyRate { get; set; }   // Tỉ lệ lấp đầy (%)
    }
}
