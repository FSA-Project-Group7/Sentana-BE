using Sentana.API.Enums;
using System.Collections.Generic;
using System;

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
        public string? TerminationReason { get; set; }
        public List<ResidentItemDto> AdditionalResidents { get; set; } = new List<ResidentItemDto>();
        public List<ServiceItemDto> SelectedServices { get; set; } = new List<ServiceItemDto>();
    }

    public class ResidentItemDto
    {
        public int AccountId { get; set; }
        public int RelationshipId { get; set; }
        public string? FullName { get; set; }
    }

    public class ServiceItemDto
    {
        public int ServiceId { get; set; }
        public decimal? ActualPrice { get; set; }
        public string? ServiceName { get; set; }
    }
}