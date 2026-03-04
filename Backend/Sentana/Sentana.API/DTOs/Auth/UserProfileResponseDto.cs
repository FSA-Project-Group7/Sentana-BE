namespace Sentana.API.DTOs.Auth
{
    public class UserProfileResponseDto
    {
        // account
        public int AccountId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }

        // info
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? BirthDay { get; set; }
        public string? Address { get; set; }
        public string? CmndCccd { get; set; }
    }
}