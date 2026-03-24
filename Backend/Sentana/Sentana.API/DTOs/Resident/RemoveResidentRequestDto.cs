using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Resident
{
    public class RemoveResidentRequestDto
    {
        [Required(ErrorMessage = "Vui lòng cung cấp AccountId.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Vui lòng cung cấp ApartmentId.")]
        public int ApartmentId { get; set; }
}