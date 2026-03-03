using Sentana.API.DTOs.Auth;

namespace Sentana.API.Services
{
    public interface IAuthService
    {
        Task<string?> LoginAsync(LoginRequestDto request);
    }
}