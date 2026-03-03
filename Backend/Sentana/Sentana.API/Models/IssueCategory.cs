using System;
using System.Collections.Generic;

namespace Sentana.API.Models;

public partial class IssueCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public byte DefaultPriority { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
}
