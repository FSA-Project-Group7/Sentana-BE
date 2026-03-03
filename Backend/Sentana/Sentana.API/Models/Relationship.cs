using System;
using System.Collections.Generic;

namespace Sentana.API.Models;

public partial class Relationship
{
    public int RelationshipId { get; set; }

    public string RelationshipName { get; set; } = null!;

    public bool? IsDeleted { get; set; }

    public virtual ICollection<ApartmentResident> ApartmentResidents { get; set; } = new List<ApartmentResident>();
}
