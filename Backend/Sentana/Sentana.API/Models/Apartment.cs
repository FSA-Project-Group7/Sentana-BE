using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class Apartment
{
    public int ApartmentId { get; set; }

    public int? BuildingId { get; set; }

    public string? ApartmentCode { get; set; }

    public string? ApartmentName { get; set; }

    public int? ApartmentNumber { get; set; }

    public int? FloorNumber { get; set; }

    public DateTime? StartDay { get; set; }

    public double? Area { get; set; }

    public ApartmentStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<ApartmentResident> ApartmentResidents { get; set; } = new List<ApartmentResident>();

    public virtual ICollection<ApartmentService> ApartmentServices { get; set; } = new List<ApartmentService>();

    public virtual Building? Building { get; set; }

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<ElectricMeter> ElectricMeters { get; set; } = new List<ElectricMeter>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();

    public virtual ICollection<WaterMeter> WaterMeters { get; set; } = new List<WaterMeter>();
}
