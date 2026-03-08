namespace Sentana.API.DTOs.Service
{
    public class RoomServiceResponseDto
    {
        public int ApartmentId { get; set; }

        public int ServiceId { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public decimal ActualPrice { get; set; }
    }
}