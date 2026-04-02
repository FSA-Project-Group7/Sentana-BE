namespace Sentana.API.DTOs.Invoice
{
    // US81 - View Monthly Revenue (Manager)
    public class MonthlyRevenueDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal TotalBilled { get; set; }     // Tổng tiền phát hành trong tháng
        public decimal TotalCollected { get; set; }  // Tổng tiền thu được trong tháng
        public decimal TotalDebt { get; set; }        // Tổng nợ còn lại trong tháng
        public int TotalInvoices { get; set; }        // Số hóa đơn
        public int PaidInvoices { get; set; }          // Số hóa đơn đã thanh toán
        public int UnpaidInvoices { get; set; }        // Số hóa đơn chưa thanh toán
    }
}
