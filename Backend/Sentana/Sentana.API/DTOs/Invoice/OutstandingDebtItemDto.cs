namespace Sentana.API.DTOs.Invoice
{
    public class OutstandingDebtItemDto
    {
        public int InvoiceId { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public string? ApartmentName { get; set; }
        public int? BillingMonth { get; set; }
        public int? BillingYear { get; set; }
        public decimal? TotalMoney { get; set; }
        public decimal? Debt { get; set; }
        public DateOnly? DayPay { get; set; }       // Ngày đến hạn
        public int DaysOverdue { get; set; }         // Số ngày đã quá hạn
        public string? Status { get; set; }
    }
}
