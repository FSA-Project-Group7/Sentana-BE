namespace Sentana.API.DTOs.Resident
{
    // US09 - View Electricity Index (Resident)
    public class ElectricityIndexDto
    {
        public int ElectricMeterId { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public decimal? OldIndex { get; set; }
        public decimal? NewIndex { get; set; }
        public decimal? Consumption { get; set; }  // NewIndex - OldIndex
        public decimal? Price { get; set; }          // Price per kWh
        public decimal? TotalAmount { get; set; }    // Consumption * Price
        public string? Status { get; set; }
    }
}
