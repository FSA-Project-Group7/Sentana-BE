using Sentana.API.DTOs.Auth;

namespace Sentana.API.Services
{
    public interface IAuthService
    {
        //login
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
        //get profile
        Task<UserProfileResponseDto?> GetUserProfileAsync(int accountId);
        //update profile
        Task<bool> UpdateUserProfileAsync(int accountId, UpdateProfileRequestDto request);

        // change and reset password
        Task<bool> SendOtpAsync(SendOtpRequestDto request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request);
    }
}