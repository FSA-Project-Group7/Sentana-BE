using Sentana.API.Helpers;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Auth
{
    public class UpdateProfileRequestDto
    {
        [Required(ErrorMessage = "Họ tên không được để trống.", AllowEmptyStrings = false)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống.", AllowEmptyStrings = false)]
        [RegularExpression(ValidationHelper.PhoneRegex, ErrorMessage = "Số điện thoại không đúng định dạng Việt Nam.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống.", AllowEmptyStrings = false)]
        [RegularExpression(ValidationHelper.EmailRegex, ErrorMessage = "Email phải có định dạng @gmail.com.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "CCCD không được để trống.", AllowEmptyStrings = false)]
        [RegularExpression(ValidationHelper.CccdRegex, ErrorMessage = "CCCD phải bao gồm đúng 12 chữ số.")]
        public string CmndCccd { get; set; } = string.Empty;

        [Required(ErrorMessage = "Địa chỉ không được để trống.", AllowEmptyStrings = false)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày sinh không được để trống.")]
        [PastDate(ErrorMessage = "Bạn chưa thể sinh ra ở tương lai được! Vui lòng chọn ngày hợp lệ.")]
        public DateTime BirthDay { get; set; }
    }
}