using Sentana.API.Enums;
using Sentana.API.Helpers;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Technician
{
    public class CreateTechnicianRequestDto
    {
        [Required(ErrorMessage = "Email không được để trống!")]
        [RegularExpression(ValidationHelper.EmailRegex, ErrorMessage = "Email bắt buộc phải có đuôi @gmail.com")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống!")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        [RegularExpression(ValidationHelper.FullNameRegex, ErrorMessage = "Họ và tên chỉ được chứa chữ cái và khoảng trắng")]
        public string FullName { get; set; } = string.Empty;

        [RegularExpression(ValidationHelper.PhoneRegex, ErrorMessage = "Số điện thoại không hợp lệ. Phải là định dạng Việt Nam 10 số")]
        public string? PhoneNumber { get; set; }

        [EnumDataType(typeof(Gender), ErrorMessage = "Giới tính không hợp lệ (0: Nam, 1: Nữ, 2: Khác).")]
        public Gender? Sex { get; set; }

        [PastDate(ErrorMessage = "Ngày sinh phải là một ngày trong quá khứ.")]
        public DateTime? BirthDay { get; set; }

        [Required(ErrorMessage = "CCCD không được để trống!")]
        [RegularExpression(ValidationHelper.CccdRegex, ErrorMessage = "CCCD bắt buộc phải có đúng 12 chữ số.")]
        public string IdentityCard { get; set; }

        public string? Country { get; set; }

        public string? City { get; set; }

        public string? Address { get; set; }
    }
}