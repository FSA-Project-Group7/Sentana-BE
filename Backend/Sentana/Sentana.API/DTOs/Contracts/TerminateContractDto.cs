using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Contracts
{
    public class TerminateContractDto
    {
        [Required(ErrorMessage = "Ngày kết thúc hợp đồng là bắt buộc.")]
        public DateOnly TerminationDate { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Chi phí thêm phải lớn hơn hoặc bằng 0.")]
        public decimal AdditionalCost { get; set; }
    }
}