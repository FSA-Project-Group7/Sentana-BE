using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Resident
{
    public class RemoveResidentRequestDto
    {
        [Required(ErrorMessage = "Vui lòng cung cấp AccountId.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Vui lòng cung cấp ApartmentId.")]
        public int ApartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do gỡ cư dân.")]
        [MinLength(1, ErrorMessage = "Lý do không được để trống.")]
        [StringLength(1000, ErrorMessage = "Lý do tối đa 1000 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }
}