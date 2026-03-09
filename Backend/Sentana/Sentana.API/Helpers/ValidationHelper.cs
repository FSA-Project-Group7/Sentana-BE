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
    }
}