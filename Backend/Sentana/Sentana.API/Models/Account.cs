using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class Account
{
    public int AccountId { get; set; }

    public string? Code { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string? Email { get; set; }

    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    public int? InfoId { get; set; }

    public int? RoleId { get; set; }

    public GeneralStatus? Status { get; set; }

    public byte? TechAvailability { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<ApartmentResident> ApartmentResidents { get; set; } = new List<ApartmentResident>();

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<History> Histories { get; set; } = new List<History>();

    public virtual InFo? Info { get; set; }

    public virtual ICollection<MaintenanceRequest> MaintenanceRequestAccounts { get; set; } = new List<MaintenanceRequest>();

    public virtual ICollection<MaintenanceRequest> MaintenanceRequestAssignedToNavigations { get; set; } = new List<MaintenanceRequest>();

    public virtual Role? Role { get; set; }
}
