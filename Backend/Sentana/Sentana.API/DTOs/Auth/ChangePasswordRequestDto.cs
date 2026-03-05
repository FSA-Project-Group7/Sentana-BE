using System.ComponentModel.DataAnnotations;
using Sentana.API.Helpers;

namespace Sentana.API.DTOs.Auth
{
    public class ChangePasswordRequestDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mã OTP từ Email.")]
        public string OtpCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu cũ.")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [RegularExpression(ValidationHelper.PasswordRegex, ErrorMessage = "Mật khẩu phải từ 8 ký tự, gồm ít nhất 1 chữ, 1 số và 1 ký tự đặc biệt.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu mới.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}