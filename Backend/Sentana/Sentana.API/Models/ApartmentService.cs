using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class ApartmentService
{
    public int Id { get; set; }

    public int? ApartmentId { get; set; }

    public int? ServiceId { get; set; }

    public DateOnly? StartDay { get; set; }

    public DateOnly? EndDay { get; set; }

    public decimal? ActualPrice { get; set; }

    public GeneralStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual Service? Service { get; set; }
}
