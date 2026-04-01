using Sentana.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Invoice
{
    public class ChangeInvoiceStatusDto
    {
        [Required(ErrorMessage = "Vui lòng chọn trạng thái mới.")]
        public InvoiceStatus Status { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string? Note { get; set; } // Hứng ghi chú để lưu vào cột ManagerNote
    }
}