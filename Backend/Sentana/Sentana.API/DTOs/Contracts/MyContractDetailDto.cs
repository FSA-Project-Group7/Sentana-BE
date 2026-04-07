using Sentana.API.Enums;
using System;
using System.Collections.Generic;

namespace Sentana.API.DTOs.Contracts
{
    public class MyContractDetailDto
    {
        public int ContractId { get; set; }
        public string? ContractCode { get; set; }
        public string? ApartmentCode { get; set; }
        public DateOnly? StartDay { get; set; }
        public DateOnly? EndDay { get; set; }
        public decimal? MonthlyRent { get; set; }
        public decimal? Deposit { get; set; }
        public GeneralStatus? Status { get; set; }
        public SettlementStatus? SettlementStatus { get; set; }
        public decimal? AdditionalCost { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? TerminationReason { get; set; }
        public DateTime? SettledAt { get; set; }

        public List<MyServiceFeeDto> Services { get; set; } = new List<MyServiceFeeDto>();

        public List<MyRoommateDto> Roommates { get; set; } = new List<MyRoommateDto>();
    }

    public class MyServiceFeeDto
    {
        public string? ServiceName { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? ActualPrice { get; set; }
        public string? Unit { get; set; }
    }

    public class MyRoommateDto
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Relationship { get; set; }
    }
}