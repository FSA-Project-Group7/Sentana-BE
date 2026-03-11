using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Invoice
{
    public class GenerateInvoiceRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tháng.")]
        [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12.")]
        public int Month { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập năm.")]
        [Range(2000, 2100, ErrorMessage = "Năm không hợp lệ (Phải từ năm 2000 trở đi).")]
        public int Year { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn căn hộ.")]
        public int ApartmentId { get; set; }
    }
}