using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public int? ApartmentId { get; set; }

    public int? ContractId { get; set; }

    public int? ElectricMeterId { get; set; }

    public int? WaterMeterId { get; set; }

    public int? BillingMonth { get; set; }

    public int? BillingYear { get; set; }

    public decimal? TotalMoney { get; set; }

    public decimal? Pay { get; set; }

    public decimal? Debt { get; set; }

    public decimal? ServiceFee { get; set; }

    public string? CodeVoucher { get; set; }

    public decimal? WaterNumber { get; set; }

    public decimal? ElectricNumber { get; set; }

    public DateOnly? DayCreat { get; set; }

    public DateOnly? DayPay { get; set; }

    public byte? Payments { get; set; }

    public InvoiceStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual Contract? Contract { get; set; }

    public virtual ElectricMeter? ElectricMeter { get; set; }

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual WaterMeter? WaterMeter { get; set; }
}
