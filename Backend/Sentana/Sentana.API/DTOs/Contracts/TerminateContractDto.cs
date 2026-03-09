using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Contracts
{
    public class TerminateContractDto
    {
        [Required(ErrorMessage = "Termination date is required")]
        public DateOnly TerminationDate { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Additional cost must be >= 0")]
        public decimal AdditionalCost { get; set; }
    }
}