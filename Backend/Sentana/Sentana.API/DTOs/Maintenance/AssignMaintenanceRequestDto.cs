using Sentana.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Maintenance
{
    public class AssignMaintenanceRequestDto
    {
        [Required(ErrorMessage = "Vui lòng cung cấp ID của kỹ thuật viên.")]
        public int TechnicianId { get; set; }

        [EnumDataType(typeof(MaintenancePriority), ErrorMessage = "Mức độ ưu tiên không hợp lệ (Chỉ chấp nhận 1: Low, 2: Medium, 3: High).")]
        public MaintenancePriority Priority { get; set; } // 1: Low, 2: Medium, 3: High
    }

}
