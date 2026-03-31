using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Utility
{
    public class InputElectricIndexDto
    {
        [Required(ErrorMessage = "Vui lòng chọn căn hộ.")]
        public int ApartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày chốt số.")]
        public DateTime RegistrationDate { get; set; }

        [Required(ErrorMessage = "Chỉ số điện không được để trống.")]
        [Range(0, double.MaxValue, ErrorMessage = "Chỉ số điện không được là số âm.")]
        public decimal NewIndex { get; set; }
        public bool IsMerge { get; set; } = false;
    }
}