using Sentana.API.Enums;

namespace Sentana.API.DTOs.Service
{
    public class UpdateServiceRequestDto
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty; 

        public decimal ServiceFee { get; set; }

        public GeneralStatus Status { get; set; }
    }
}