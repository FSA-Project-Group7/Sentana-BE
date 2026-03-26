using Sentana.API.DTOs.Auth;

namespace Sentana.API.Services.SBuilding
{
    public interface IAuthService
    {
        //login
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
        //get profile
        Task<UserProfileResponseDto?> GetUserProfileAsync(int accountId);
        //update profile
        Task<(bool IsSuccess, string Message)> UpdateUserProfileAsync(int accountId, UpdateProfileRequestDto request);

        // change and reset password
        Task<bool> SendOtpAsync(SendOtpRequestDto request);

        Task<bool> RequestChangePasswordOtpAsync(int accountId);
        Task<bool> ChangePasswordAsync(int accountId, ChangePasswordRequestDto request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request);
        Task<TokenModelDto?> RenewTokenAsync(TokenModelDto request);
        Task<bool> LogoutAsync(int accountId);

        Task<bool> SetupPasswordAsync(int accountId, SetupPasswordRequestDto request);
    }
}