using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class PaymentTransaction
{
    public int TransactionId { get; set; }

    public int? InvoiceId { get; set; }

    public decimal? AmountPaid { get; set; }

    public string? PaymentProofImage { get; set; }

    public DateTime? SubmitDate { get; set; }

    public PaymentTransactionStatus? Status { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Invoice? Invoice { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }
}
