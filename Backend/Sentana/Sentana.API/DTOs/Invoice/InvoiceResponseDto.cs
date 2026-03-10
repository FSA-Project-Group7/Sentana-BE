namespace Sentana.API.DTOs.Invoice
{
    // class dùng để hiển thị từng dòng phí 
    public class InvoiceDetailItemDto
    {
        public string FeeName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class InvoiceResponseDto
    {
        public int InvoiceId { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public int? ContractId { get; set; }
        public int? BillingMonth { get; set; }
        public int? BillingYear { get; set; }
        public decimal? TotalMoney { get; set; }
        public decimal? ServiceFee { get; set; }
        public decimal? Pay { get; set; }
        public decimal? Debt { get; set; }
        public decimal? WaterNumber { get; set; }
        public decimal? ElectricNumber { get; set; }
        public string? DayCreat { get; set; }
        public string? DayPay { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public byte? Payments { get; set; }

        // danh sách chi tiết từng khoản thu
        public List<InvoiceDetailItemDto> Details { get; set; } = new List<InvoiceDetailItemDto>();
    }
}