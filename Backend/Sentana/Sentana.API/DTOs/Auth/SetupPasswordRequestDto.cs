using Sentana.API.Helpers;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Auth
{
    public class SetupPasswordRequestDto
    {
        [Required(ErrorMessage = "Mật khẩu cũ không được để trống.")]
        public string OldPassword { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmNewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Họ tên không được để trống.", AllowEmptyStrings = false)]
        [RegularExpression(ValidationHelper.FullNameRegex, ErrorMessage = "Họ tên không hợp lệ. Chỉ được phép nhập chữ cái và khoảng trắng, không chứa số hoặc ký tự đặc biệt.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống.", AllowEmptyStrings = false)]
        [RegularExpression(ValidationHelper.PhoneRegex, ErrorMessage = "Số điện thoại không đúng định dạng Việt Nam.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống.", AllowEmptyStrings = false)]
        [RegularExpression(ValidationHelper.EmailRegex, ErrorMessage = "Email phải có định dạng @gmail.com.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày sinh không được để trống.")]
        [PastDate(ErrorMessage = "Bạn chưa thể sinh ra ở tương lai được! Vui lòng chọn ngày hợp lệ.")]
        public DateTime BirthDay { get; set; }
    }
}
