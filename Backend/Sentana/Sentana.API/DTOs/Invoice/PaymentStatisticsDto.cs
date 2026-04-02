namespace Sentana.API.DTOs.Invoice
{
    // US14 - View Payment Statistics (Manager)
    public class PaymentStatisticsDto
    {
        public int TotalInvoices { get; set; }
        public int PaidInvoices { get; set; }
        public int UnpaidInvoices { get; set; }
        public int PendingVerificationInvoices { get; set; }
        public decimal TotalRevenue { get; set; }          // Tổng tiền đã thu
        public decimal TotalDebt { get; set; }              // Tổng nợ còn lại
        public decimal TotalBilled { get; set; }            // Tổng tiền đã phát hành

        // Thống kê theo căn hộ
        public List<ApartmentPaymentStatDto> ByApartment { get; set; } = new();
    }

    public class ApartmentPaymentStatDto
    {
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public int TotalInvoices { get; set; }
        public int PaidInvoices { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalDebt { get; set; }
    }
}
