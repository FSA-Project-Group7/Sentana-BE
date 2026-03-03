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
    public class AuthController : ControllerBase
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
    }
}
