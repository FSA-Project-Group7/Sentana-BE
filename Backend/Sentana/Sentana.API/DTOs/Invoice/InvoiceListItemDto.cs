namespace Sentana.API.DTOs.Invoice
{
    public class InvoiceListItemDto
    {
        public int InvoiceId { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public int? BillingMonth { get; set; }
        public int? BillingYear { get; set; }
        public decimal? TotalMoney { get; set; }
        public decimal? Debt { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? CreatedAt { get; set; }
        public string? BillingPeriod { get; set; }
    }
}