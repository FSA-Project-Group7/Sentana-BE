namespace Sentana.API.DTOs.Resident
{
    // US11 - View Service Fees (Resident)
    public class ServiceFeeDto
    {
        public int Id { get; set; }
        public int? ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public string? Description { get; set; }
        public decimal? ActualPrice { get; set; }
        public DateOnly? StartDay { get; set; }
        public DateOnly? EndDay { get; set; }
        public string? Status { get; set; }
    }
}
