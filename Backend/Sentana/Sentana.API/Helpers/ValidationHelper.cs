namespace Sentana.API.Helpers
{
    public static class ValidationHelper
    {
        // Email kết thúc bằng @gmail.com
        public const string EmailRegex = @"^[a-zA-Z0-9._%+-]+@gmail\.com$";

        // SDT Việt Nam
        public const string PhoneRegex = @"^(0|84|\+84)[35789][0-9]{8}$";

        // CCCD 12 chữ số
        public const string CccdRegex = @"^[0-9]{12}$";

        // Mật khẩu it nhất 8 ký tự, 1 chữ cái, 1 chữ số, 1 ký tự đặc biệt
        public const string PasswordRegex = @"^(?=.*[A-Za-z])(?=.*\d)(?=.*[@$!%*#?&])[A-Za-z\d@$!%*#?&]{8,}$";

        // Họ và tên chỉ chữ cái
        public const string FullNameRegex = @"^[\p{L}\s]+$";

        // Validate chỉ số nhập liệu cho input index điện nước
        public static (bool IsValid, string ErrorMessage) ValidateUtilityIndex(decimal newIndex, decimal oldIndex, DateTime registrationDate)
        {
            // kiểm tra ngày tháng: Không được ở tương lai
            if (registrationDate > DateTime.Now)
            {
                return (false, "Định dạng ngày hợp lệ nhưng ngày chốt số không được lớn hơn ngày hiện tại.");
            }

            // kiểm tra logic con số: Không được âm
            if (newIndex < 0)
            {
                return (false, "Chỉ số tiêu thụ không được phép là số âm.");
            }

            // số mới phải lớn hơn hoặc bằng số cũ
            if (newIndex < oldIndex)
            {
                return (false, $"Chỉ số mới ({newIndex}) không được nhỏ hơn chỉ số cũ ({oldIndex}).");
            }

            return (true, string.Empty);
        }
        // Validate thời gian tháng năm
        public static (bool IsValid, string ErrorMessage) ValidateMonthYear(int? month, int? year)
        {
            if (month.HasValue && (month < 1 || month > 12))
            {
                return (false, "Tháng phải nằm trong khoảng từ 1 đến 12.");
            }
            if (year.HasValue && year < 1)
            {
                return (false, "Năm không hợp lệ (phải lớn hơn 0).");
            }
            if (month.HasValue && year.HasValue)
            {
                var currentDate = DateTime.Now;
                if (year > currentDate.Year || (year == currentDate.Year && month > currentDate.Month))
                {
                    return (false, "Không thể thao tác với thời gian trong tương lai.");
                }
            }
            return (true, string.Empty);
        }

        // Generate password 
        public static string GenerateRandomPassword(int length = 10)
        {
            const string letters = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string specials = "@$!%*#?&";
            var random = new Random();
            var passwordChars = new char[length];
            passwordChars[0] = letters[random.Next(letters.Length)];
            passwordChars[1] = digits[random.Next(digits.Length)];
            passwordChars[2] = specials[random.Next(specials.Length)];
            const string allChars = letters + digits + specials;
            for (int i = 3; i < length; i++)
            {
                passwordChars[i] = allChars[random.Next(allChars.Length)];
            }
            return new string(passwordChars.OrderBy(x => random.Next()).ToArray());
        }
    }
}