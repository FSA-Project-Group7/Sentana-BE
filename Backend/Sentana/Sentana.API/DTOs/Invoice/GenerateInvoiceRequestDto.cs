using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Invoice
{
    public class GenerateInvoiceRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tháng.")]
        [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12.")]
        public int Month { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập năm.")]
        public int Year { get; set; }

        public int? ApartmentId { get; set; }
    }
}