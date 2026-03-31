using Sentana.API.Enums;

namespace Sentana.API.DTOs.Maintenance
{
    public class AssignMaintenanceRequestDto
    {
        public int TechnicianId { get; set; }
        public MaintenancePriority Priority { get; set; } // 1: Low, 2: Medium, 3: High
    }

}
