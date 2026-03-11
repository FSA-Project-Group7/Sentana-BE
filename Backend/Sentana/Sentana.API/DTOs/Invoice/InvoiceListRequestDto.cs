using System.ComponentModel.DataAnnotations;
using Sentana.API.Enums;

namespace Sentana.API.DTOs.Invoice
{
    public class InvoiceListRequestDto
    {
        [Range(1, 12, ErrorMessage = "Tháng không hợp lệ.")]
        public int? Month { get; set; }

        [Range(2000, 2100, ErrorMessage = "Năm không hợp lệ.")]
        public int? Year { get; set; }

        // lọc theo trạng thái Unpaid, Pending, Paid
        public InvoiceStatus? Status { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Trang hiện tại (PageNumber) phải lớn hơn 0.")]
        public int PageNumber { get; set; } = 1;

        [Required]
        [Range(1, 100, ErrorMessage = "Số lượng hiển thị (PageSize) phải từ 1 đến 100.")]
        public int PageSize { get; set; } = 10;
    }
}