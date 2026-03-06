namespace Sentana.API.DTOs.Service
{
    public class ServiceResponseDto
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; }

        public string Description { get; set; }

        public decimal ServiceFee { get; set; }

        public int Status { get; set; }
    }
}