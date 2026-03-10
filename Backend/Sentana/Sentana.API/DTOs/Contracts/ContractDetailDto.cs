using Sentana.API.Enums;

namespace Sentana.API.DTOs.Contracts
{
    public class ContractDetailDto
    {
        public int ContractId { get; set; }

        public string? ContractCode { get; set; }

        public int? ApartmentId { get; set; }

        public string? ApartmentName { get; set; }

        public int? AccountId { get; set; }

        public string? TenantName { get; set; }

        public DateOnly? StartDay { get; set; }

        public DateOnly? EndDay { get; set; }

        public decimal? MonthlyRent { get; set; }

        public decimal? Deposit { get; set; }

        public decimal? AdditionalCost { get; set; }

        public decimal? RefundAmount { get; set; }

        public string? File { get; set; }

        public GeneralStatus? Status { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}