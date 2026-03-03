using Sentana.API.Helpers;
using System.ComponentModel.DataAnnotations;

namespace Sentana.API.DTOs.Auth
{
    public class UpdateProfileRequestDto
    {
        public string? FullName { get; set; }

        [RegularExpression(ValidationHelper.PhoneRegex, ErrorMessage = "Số điện thoại không đúng định dạng.")]
        public string? PhoneNumber { get; set; }

        [RegularExpression(ValidationHelper.EmailRegex, ErrorMessage = "Email phải có định dạng @gmail.com.")]
        public string? Email { get; set; }

        [RegularExpression(ValidationHelper.CccdRegex, ErrorMessage = "CCCD phải bao gồm đúng 12 chữ số.")]
        public string? CmndCccd { get; set; }

        public string? Address { get; set; }
        public DateTime? BirthDay { get; set; }
    }
}