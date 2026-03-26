using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Contracts
{
    public class UpdateContractDto
    {
        public DateOnly? StartDay { get; set; }

        public DateOnly? EndDay { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tiền thuê phải >= 0.")]
        public decimal? MonthlyRent { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tiền cọc phải >= 0.")]
        public decimal? Deposit { get; set; }

        [DataType(DataType.Upload)]
        public IFormFile? File { get; set; }
        public List<ResidentItemDto>? AdditionalResidents { get; set; }
        public List<ServiceItemDto>? Services { get; set; }
    }
}