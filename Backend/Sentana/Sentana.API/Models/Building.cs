using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class Building
{
    public int BuildingId { get; set; }

    public string? BuildingName { get; set; }

    public string? BuildingCode { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public int? FloorNumber { get; set; }

    public int? ApartmentNumber { get; set; }

    public GeneralStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();

    public int? ManagerId { get; set; }

    public virtual Account? Manager { get; set; }
}
