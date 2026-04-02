namespace Sentana.API.DTOs.Resident
{
    // US10 - View Water Index (Resident)
    public class WaterIndexDto
    {
        public int WaterMeterId { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public decimal? OldIndex { get; set; }
        public decimal? NewIndex { get; set; }
        public decimal? Consumption { get; set; }  // NewIndex - OldIndex
        public decimal? Price { get; set; }          // Price per m³
        public decimal? TotalAmount { get; set; }    // Consumption * Price
        public string? Status { get; set; }
    }
}
