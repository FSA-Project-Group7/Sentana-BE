using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class ApartmentResident
{
    public int ResidentId { get; set; }

    public int? ApartmentId { get; set; }

    public int? AccountId { get; set; }

    public int? RelationshipId { get; set; }

    public GeneralStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Account? Account { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual Relationship? Relationship { get; set; }
}

