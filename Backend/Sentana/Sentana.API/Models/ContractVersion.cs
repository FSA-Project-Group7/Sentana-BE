using System;

namespace Sentana.API.Models
{
    public partial class ContractVersion
    {
        public int VersionId { get; set; }

        public int ContractId { get; set; }

        public decimal VersionNumber { get; set; }

        public string File { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedBy { get; set; }

        public string? Note { get; set; }

        public bool IsDeleted { get; set; } = false;

        public virtual Contract Contract { get; set; } = null!;
    }
}