using Sentana.API.Enums;

namespace Sentana.API.DTOs.Building
{
    public class CreateBuildingDto
    {
        public string? BuildingName { get; set; }
        public string? BuildingCode { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public int? FloorNumber { get; set; }
        public int? ApartmentNumber { get; set; }
        public GeneralStatus? Status { get; set; }
    }
}

