using Sentana.API.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sentana.API.Models;

public partial class InFo
{
    public int InfoId { get; set; }

    public string? FullName { get; set; }

    [Column(TypeName = "date")]
    public DateTime? BirthDay { get; set; }

    public Gender? Sex { get; set; }

    public string? CmndCccd { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }

    public string? Address { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
