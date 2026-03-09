using Sentana.API.Enums;

namespace Sentana.API.DTOs.Technician
{
    public class TechnicianResponseDto
    {
        public int AccountId { get; set; }
        public string? UserName { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public GeneralStatus? Status { get; set; }
        public byte? TechAvailability { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
