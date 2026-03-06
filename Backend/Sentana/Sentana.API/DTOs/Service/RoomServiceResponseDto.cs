namespace Sentana.API.DTOs.Service
{
    public class RoomServiceResponseDto
    {
        public int ApartmentId { get; set; }

        public int ServiceId { get; set; }

        public string ServiceName { get; set; }

        public decimal? ActualPrice { get; set; }
    }
}