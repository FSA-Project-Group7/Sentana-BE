namespace Sentana.API.DTOs.Payment
{
    public class PaymentHistoryItemDto
    {
        public int InvoiceId { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public int? BillingMonth { get; set; }
        public int? BillingYear { get; set; }
        public decimal? TotalMoney { get; set; }
        public decimal AmountPaid { get; set; }
        public string? PaidDate { get; set; }
    }
}

