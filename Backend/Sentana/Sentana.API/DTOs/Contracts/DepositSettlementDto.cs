namespace Sentana.API.DTOs.Contracts
{
    // US21 - View Deposit Settlement
    public class DepositSettlementDto
    {
        public int ContractId { get; set; }
        public string? ContractCode { get; set; }
        public int? ApartmentId { get; set; }
        public string? ApartmentCode { get; set; }
        public string? ApartmentName { get; set; }

        // Thông tin chủ hợp đồng
        public int? ResidentAccountId { get; set; }
        public string? ResidentName { get; set; }
        public string? ResidentEmail { get; set; }

        // Thông tin tài chính
        public decimal? Deposit { get; set; }           // Số tiền đặt cọc gốc
        public decimal? AdditionalCost { get; set; }    // Chi phí phát sinh (đền bù hư hỏng, ...)
        public decimal? RefundAmount { get; set; }      // Số tiền hoàn trả cho cư dân

        // Thông tin hợp đồng
        public DateOnly? StartDay { get; set; }
        public DateOnly? EndDay { get; set; }
        public decimal? MonthlyRent { get; set; }
        public string? Status { get; set; }
        public string? TerminationReason { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
