using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Maintenance
{
    public class RejectTaskRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do từ chối nghiệm thu.")]
        public string RejectReason { get; set; } = null!;
    }
}
