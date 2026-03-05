namespace Sentana.API.DTOs.Service
{
    public class UpdateRoomServicePriceRequestDto
    {
        public int ApartmentId { get; set; }

        public int ServiceId { get; set; }

        public decimal ActualPrice { get; set; }
    }
}