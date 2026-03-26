namespace Sentana.API.DTOs.Auth
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty; // Thêm dòng này
        public string Role { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int AccountId { get; set; }

        public bool RequiresPasswordChange { get; set; }
    }
}