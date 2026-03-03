using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class MaintenanceRequest
{
    public int RequestId { get; set; }

    public int? AccountId { get; set; }

    public int? ApartmentId { get; set; }

    public int? CategoryId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public byte? Priority { get; set; }

    public DateTime? CreateDay { get; set; }

    public DateTime? FixDay { get; set; }

    public int? AssignedTo { get; set; }

    public MaintenanceRequestStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Account? Account { get; set; }

    public virtual Apartment? Apartment { get; set; }

    public virtual Account? AssignedToNavigation { get; set; }

    public virtual IssueCategory? Category { get; set; }
}
