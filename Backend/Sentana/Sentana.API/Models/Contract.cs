using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class Contract
{
    public int ContractId { get; set; }

    public string? ContractCode { get; set; }

    public int? ApartmentId { get; set; }

    public int? AccountId { get; set; }

    public DateOnly? StartDay { get; set; }

    public DateOnly? EndDay { get; set; }

    public decimal? MonthlyRent { get; set; }

    public decimal? Deposit { get; set; }

    public decimal? AdditionalCost { get; set; }

    public decimal? RefundAmount { get; set; }

    public string? File { get; set; }

    public GeneralStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Account? Account { get; set; }

    public virtual Apartment? Apartment { get; set; }
    public string? TerminationReason { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public int? CurrentVersionId { get; set; }

    public virtual ContractVersion? CurrentVersion { get; set; }
    public SettlementStatus? SettlementStatus { get; set; } = Sentana.API.Enums.SettlementStatus.NotRequired; public DateTime? SettledAt { get; set; }

    public virtual ICollection<ContractVersion> ContractVersions { get; set; }
        = new List<ContractVersion>();
}
