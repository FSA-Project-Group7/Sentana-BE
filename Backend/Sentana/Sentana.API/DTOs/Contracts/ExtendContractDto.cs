using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Contracts
{
    public class ExtendContractDto
    {
        [Required(ErrorMessage = "Vui lòng nhập ngày kết thúc mới.")]
        public DateOnly NewEndDate { get; set; }
    }
}