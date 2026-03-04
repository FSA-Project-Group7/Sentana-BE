namespace Sentana.API.DTOs.Service
{
    public class CreateServiceRequestDto
    {
        public string ServiceName { get; set; }

        public string Description { get; set; }

        public decimal ServiceFee { get; set; }
    }
}