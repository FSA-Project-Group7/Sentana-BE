using Sentana.API.Enums;
using Sentana.API.Helpers;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Technician
{
    public class UpdateTechnicianRequestDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [RegularExpression(ValidationHelper.EmailRegex, ErrorMessage = "Email bắt buộc phải có đuôi @gmail.com")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        [RegularExpression(ValidationHelper.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ. Phải là định dạng VN 10 số")]
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
