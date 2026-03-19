using Sentana.API.Enums;

namespace Sentana.API.DTOs.Info
{
    public class InfoCheckResponseDto
    {
        public int InfoId { get; set; }
        public string FullName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public Gender? Sex { get; set; }
        public DateTime? Birthday { get; set; }
    }
}
