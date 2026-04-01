using Sentana.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Invoice
{
    public class ChangeInvoiceStatusDto
    {
        [Required(ErrorMessage = "Vui lòng cung cấp trạng thái.")]
        [EnumDataType(typeof(InvoiceStatus), ErrorMessage = "Trạng thái hóa đơn không tồn tại trong hệ thống.")]
        public InvoiceStatus Status { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string? Note { get; set; }
    }
}