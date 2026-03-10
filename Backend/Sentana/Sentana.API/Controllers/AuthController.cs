using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Auth;
using Sentana.API.Helpers;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : BaseController
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var loginResult = await _authService.LoginAsync(request);

            if (loginResult == null)
            {
                return Unauthorized(ApiResponse<string>.Fail(401, "Sai tài khoản hoặc mật khẩu."));
            }

            // Trả về object loginResult chứa cả Token và Role
            return Ok(ApiResponse<LoginResponseDto>.Success(loginResult, "Đăng nhập thành công!"));
        }

        // test băm mật khẩu
        [HttpGet("generate-hash")]
        public IActionResult GenerateHash(string passwordText = "123123")
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(passwordText);

            return Ok(new
            {
                Original = passwordText,
                HashedPassword = hash
            });
        }

        [HttpGet("profile")]
        [Authorize] // jwt token
        public async Task<IActionResult> GetMyProfile()
        {
            // lấy AccountId từ token
            var accountIdClaim = User.FindFirstValue("AccountId");

            if (!int.TryParse(accountIdClaim, out int accountId))
            {
                return Unauthorized(ApiResponse<string>.Fail(401, "Token không hợp lệ."));
            }

            // AuthService
            var profile = await _authService.GetUserProfileAsync(accountId);

            if (profile == null)
            {
                return NotFound(ApiResponse<string>.Fail(404, "Không tìm thấy người dùng."));
            }

            return Ok(ApiResponse<UserProfileResponseDto>.Success(profile, "Lấy thông tin thành công!"));
        }


        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
        {
            int accountId = GetCurrentAccountId();

            var result = await _authService.UpdateUserProfileAsync(accountId, request);

            if (!result.IsSuccess)
            {
                return BadRequest(ApiResponse<string>.Fail(400, result.Message));
            }

            return Ok(ApiResponse<string>.Success(null, "Cập nhật thông tin cá nhân thành công!"));
        }

        //Reset and change password
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequestDto request)
        {
            var result = await _authService.SendOtpAsync(request);
            if (!result) return NotFound(ApiResponse<string>.Fail(404, "Không tìm thấy tài khoản với Email này."));

            return Ok(ApiResponse<string>.Success(null, "Đã gửi mã OTP về Email của bạn!"));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request);
                if (!result) return BadRequest(ApiResponse<string>.Fail(400, "Đổi mật khẩu thất bại."));

                return Ok(ApiResponse<string>.Success(null, "Đổi mật khẩu thành công! Bạn có thể đăng nhập bằng mật khẩu mới."));
            }
            catch (Exception ex) // Bắt lỗi OTP sai hoặc hết hạn từ Service ném ra
            {
                return BadRequest(ApiResponse<string>.Fail(400, ex.Message));
            }
        }

        [HttpPost("request-change-password-otp")]
        [Authorize]
        public async Task<IActionResult> RequestChangePasswordOtp()
        {
            var accountIdClaim = User.FindFirstValue("AccountId");
            if (!int.TryParse(accountIdClaim, out int accountId))
                return Unauthorized(ApiResponse<string>.Fail(401, "Token không hợp lệ."));

            var result = await _authService.RequestChangePasswordOtpAsync(accountId);

            if (!result) return BadRequest(ApiResponse<string>.Fail(400, "Không thể gửi OTP. Tài khoản có thể chưa cập nhật Email."));

            return Ok(ApiResponse<string>.Success(null, "Đã gửi mã OTP xác nhận về Email của bạn."));
        }

        [HttpPut("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            try
            {
                var accountIdClaim = User.FindFirstValue("AccountId");
                if (!int.TryParse(accountIdClaim, out int accountId))
                    return Unauthorized(ApiResponse<string>.Fail(401, "Token không hợp lệ."));

                var result = await _authService.ChangePasswordAsync(accountId, request);

                if (!result) return BadRequest(ApiResponse<string>.Fail(400, "Đổi mật khẩu thất bại."));

                return Ok(ApiResponse<string>.Success(null, "Đổi mật khẩu thành công!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail(400, ex.Message));
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenModelDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.AccessToken) || string.IsNullOrEmpty(request.RefreshToken))
            {
                return BadRequest(ApiResponse<string>.Fail(400, "Vui lòng cung cấp đầy đủ Access Token và Refresh Token."));
            }

            try
            {
                var result = await _authService.RenewTokenAsync(request);

                if (result == null)
                {
                    return BadRequest(ApiResponse<string>.Fail(400, "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại."));
                }

                return Ok(ApiResponse<TokenModelDto>.Success(result, "Làm mới phiên đăng nhập thành công!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail(400, "Token sai định dạng: " + ex.Message));
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            int accountId = GetCurrentAccountId();

            var result = await _authService.LogoutAsync(accountId);

            if (!result)
            {
                return BadRequest(ApiResponse<string>.Fail(400, "Đăng xuất thất bại."));
            }

            return Ok(ApiResponse<string>.Success(null, "Đăng xuất thành công!"));
        }
    }
}
