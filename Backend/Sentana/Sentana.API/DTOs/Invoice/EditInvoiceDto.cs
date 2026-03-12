using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Invoice
{
    public class EditInvoiceDto
    {
        [Range(0, double.MaxValue, ErrorMessage = "Phụ phí không được là số âm.")]
        public decimal? AdditionalFee { get; set; }

        public string? Note { get; set; } // Lý do thêm phụ phí
    }
}