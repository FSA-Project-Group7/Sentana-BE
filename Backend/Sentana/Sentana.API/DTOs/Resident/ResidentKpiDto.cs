namespace Sentana.API.DTOs.Resident
{
    // US80 - View Total Residents KPI (Manager)
    public class ResidentKpiDto
    {
        public int TotalResidents { get; set; }           // Tổng số cư dân (có tài khoản)
        public int ActiveResidents { get; set; }          // Đang hoạt động
        public int InactiveResidents { get; set; }        // Bị khóa
        public int ResidentsInRoom { get; set; }          // Đang ở trong phòng
        public int ResidentsNotInRoom { get; set; }       // Chưa có phòng
        public int NewResidentsThisMonth { get; set; }    // Cư dân mới trong tháng hiện tại

        public List<BuildingResidentCountDto> ByBuilding { get; set; } = new();
    }

    public class BuildingResidentCountDto
    {
        public int BuildingId { get; set; }
        public string? BuildingName { get; set; }
        public string? BuildingCode { get; set; }
        public int ResidentCount { get; set; }
    }
}
