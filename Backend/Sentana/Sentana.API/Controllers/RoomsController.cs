using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Resident;
using Sentana.API.Helpers;
using System.Security.Claims;

namespace Sentana.API.Controllers
{
    [ApiController]
    [Route("api/rooms")]
    [Authorize(Roles = "Resident")]
    public class RoomsController : ControllerBase
    {
        private readonly ResidentService _residentService;

        public RoomsController(ResidentService residentService)
        {
            _residentService = residentService;
        }

        [HttpGet("my-room")]
        public async Task<IActionResult> GetMyRoom()
        {
            var accountIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(accountIdStr) || !int.TryParse(accountIdStr, out int accountId) || accountId <= 0)
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại."));
            }

            try
            {
                var result = await _residentService.GetMyRoomAsync(accountId);

                if (result == null)
                {
                    return NotFound(ApiResponse<object>.Fail(404, "Bạn chưa được gán vào căn hộ nào."));
                }

                return Ok(ApiResponse<MyRoomResponseDto>.Success(result, "Lấy thông tin căn hộ thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(403, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"Lỗi hệ thống: {ex.Message}"));
            }
        }
    }
}
