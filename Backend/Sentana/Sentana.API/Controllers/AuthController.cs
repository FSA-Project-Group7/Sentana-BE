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
            // gọi service 
            var token = await _authService.LoginAsync(request);

            // if null = sai thông tin
            if (token == null)
            {
                return Unauthorized(ApiResponse<string>.Fail(401, "Sai tài khoản hoặc mật khẩu."));
            }

            // trả token
            return Ok(ApiResponse<string>.Success(token, "Đăng nhập thành công!"));
        }
    }
}