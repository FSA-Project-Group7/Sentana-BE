using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class ElectricMeter
{
    public int ElectricMeterId { get; set; }

    public int? ApartmentId { get; set; }

    public DateTime? RegistrationDate { get; set; }

    public string? Code { get; set; }

    public DateTime? DeadlineDate { get; set; }

    public decimal? OldIndex { get; set; }

    public decimal? NewIndex { get; set; }

    public decimal? Price { get; set; }

    public GeneralStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
