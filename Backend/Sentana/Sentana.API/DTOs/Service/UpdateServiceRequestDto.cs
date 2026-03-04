using Sentana.API.Enums;

namespace Sentana.API.DTOs.Service
{
    public class UpdateServiceRequestDto
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; }

        public string Description { get; set; }

        public decimal ServiceFee { get; set; }

        public GeneralStatus Status { get; set; }
    }
}