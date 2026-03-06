using Sentana.API.Enums;

namespace Sentana.API.DTOs.Building
{
    public class BuildingResponseDto
    {
        public int BuildingId { get; set; }
        public string? BuildingName { get; set; }
        public string? BuildingCode { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public int? FloorNumber { get; set; }
        public int? ApartmentNumber { get; set; }
        public string StatusName { get; set; } = string.Empty;
    }
}