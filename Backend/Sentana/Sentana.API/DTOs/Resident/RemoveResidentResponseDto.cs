using System;

namespace Sentana.API.DTOs.Resident
{
    public class RemoveResidentResponseDto
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; } = string.Empty;

        // ids for verification in client / tests
        public int? ApartmentResidentId { get; set; }
        public int? ApartmentId { get; set; }
        public int? AccountId { get; set; }
        public int? ContractId { get; set; }

        // resulting statuses (string to avoid coupling with enums in DTO)
        public string? ApartmentStatus { get; set; }
        public string? ResidentStatus { get; set; }
        public string? ContractStatus { get; set; }

        public DateTime? RemovedAt { get; set; }
        public int? RemovedBy { get; set; }
    }
}
