using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Technician
{
    public class TechnicianRequestDto
    {
        [Required(ErrorMessage ="Email không được để trống!")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng!")]
        public string Email { get; set; }

        [Required(ErrorMessage ="Tên đăng nhập không được để trống!")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải ít nhất 6 kí tự")]
        public string Password { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }

    }
}
