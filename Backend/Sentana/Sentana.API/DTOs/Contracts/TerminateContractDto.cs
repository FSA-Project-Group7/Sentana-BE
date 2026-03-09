using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Contracts
{
    public class TerminateContractDto
    {
        [Required]
        public DateOnly TerminationDate { get; set; }
    }
}