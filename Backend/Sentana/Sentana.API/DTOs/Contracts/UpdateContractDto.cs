using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Contracts
{
    public class UpdateContractDto
    {
        [Required(ErrorMessage = "Apartment ID là bắt buộc.")]
        public int ApartmentId { get; set; }

        [Required(ErrorMessage = "Account ID là bắt buộc.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Ngày bắt đầu hợp đồng là bắt buộc.")]
        public DateOnly StartDay { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc hợp đồng là bắt buộc.")]
        public DateOnly EndDay { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tiền thuê phải >= 0.")]
        public decimal MonthlyRent { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tiền cọc phải >= 0.")]
        public decimal Deposit { get; set; }

        public string? File { get; set; }
    }
}