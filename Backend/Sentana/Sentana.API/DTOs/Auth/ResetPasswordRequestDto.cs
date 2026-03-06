using System.ComponentModel.DataAnnotations;
using Sentana.API.Helpers;

namespace Sentana.API.DTOs.Auth
{
    public class ResetPasswordRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;

        [Required(ErrorMessage =" Mật khẩu không được để trống", AllowEmptyStrings = false)]
        [RegularExpression(ValidationHelper.PasswordRegex, ErrorMessage = "Mật khẩu phải từ 8 ký tự, gồm ít nhất 1 chữ, 1 số và 1 ký tự đặc biệt.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}