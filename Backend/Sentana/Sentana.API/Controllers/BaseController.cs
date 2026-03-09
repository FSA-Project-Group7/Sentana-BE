using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        // Lấy AccountId của người dùng đang gọi API từ JWT Token
        protected int GetCurrentAccountId()
        {
            var accountIdClaim = User.FindFirstValue("AccountId");
            if (int.TryParse(accountIdClaim, out int accountId))
            {
                return accountId;
            }

            throw new UnauthorizedAccessException("Token không hợp lệ hoặc phiên đăng nhập đã hết hạn.");
        }

        // Lấy Role của người dùng đang gọi API
        protected string GetCurrentUserRole()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }
    }
}