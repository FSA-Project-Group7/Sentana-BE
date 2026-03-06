using Sentana.API.Helpers;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Technician
{
    public class CreateTechnicianRequestDto
    {
        [Required(ErrorMessage ="Email không được để trống!")]
        [RegularExpression(ValidationHelper.EmailRegex, ErrorMessage = "Email bắt buộc phải có đuôi @gmail.com")]
        public string Email { get; set; }

        [Required(ErrorMessage ="Tên đăng nhập không được để trống!")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [RegularExpression(ValidationHelper.PasswordRegex, ErrorMessage = "Mật khẩu phải ít nhất 8 ký tự, gồm chữ cái, chữ số và ký tự đặc biệt")]
        public string Password { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        [RegularExpression(ValidationHelper.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ. Phải là định dạng VN 10 số")]
        public string? PhoneNumber { get; set; }

    }
}
