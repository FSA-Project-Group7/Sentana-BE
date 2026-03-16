using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Payment
{
    public class RejectPaymentDto
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do từ chối để Cư dân biết cách khắc phục.")]
        [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
        public string Reason { get; set; }
    }
}