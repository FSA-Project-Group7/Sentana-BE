namespace Sentana.API.DTOs.Auth
{
    public class TokenModelDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}