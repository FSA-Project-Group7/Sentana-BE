namespace Sentana.API.DTOs.Resident
{
    public class MyRoomResponseDto
    {
        public int ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public string? ApartmentName { get; set; }
        public int? ApartmentNumber { get; set; }
        public int? FloorNumber { get; set; }
        public double? Area { get; set; }
        public string? Status { get; set; }
        public List<RoommateDto> Roommates { get; set; } = new();
    }
}
