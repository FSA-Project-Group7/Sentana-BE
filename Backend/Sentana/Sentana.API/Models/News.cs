using System;
using System.Collections.Generic;
using Sentana.API.Enums;

namespace Sentana.API.Models;

public partial class News
{
    public int NewsId { get; set; }

    public string? Title { get; set; }

    public string? Slug { get; set; }

    public string? Image { get; set; }

    public string? Description { get; set; }

    public DateOnly? CreateDay { get; set; }

    public GeneralStatus? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public bool? IsDeleted { get; set; }
}
