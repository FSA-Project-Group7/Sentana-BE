using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Maintenance;
using Sentana.API.Helpers;
using Sentana.API.Services.SMaintenance;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaintenanceController : ControllerBase
    {
        private readonly IMaintenanceService _maintenanceService;

        public MaintenanceController(IMaintenanceService maintenanceService)
        {
            _maintenanceService = maintenanceService;
        }

        private int GetCurrentUserId()
        {
            var accountIdClaim = User.FindFirstValue("AccountId");
            int.TryParse(accountIdClaim, out int currentUserId);
            return currentUserId;
        }
        [HttpGet("my-apartments")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> GetMyApartments()
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.GetMyActiveApartmentsAsync(userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message, result.Data });
        }

        [HttpPost("requests")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> CreateResidentRequest([FromForm] CreateMaintenanceDto request)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.CreateResidentRequestAsync(request, userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message, result.Data });
        }

        [HttpGet("my-requests")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.GetResidentRequestsAsync(userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message, result.Data });
        }

        [HttpGet("assigned-to-me")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> GetMyTasks([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.GetMyAssignedTasksAsync(userId, pageIndex, pageSize);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message, result.Data });
        }

        [HttpPut("{id}/accept")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> AcceptTask(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.AcceptTaskAsync(id, userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message });
        }

        [HttpPut("{id}/start")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> StartTask(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.StartProcessingTaskAsync(id, userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message });
        }

        [HttpPut("{id}/fix")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> FixTask(int id, [FromBody] FixTaskRequestDto request)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.FixTaskAsync(id, request, userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message });
        }

        [HttpGet("manager/request")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetRequestsForManager([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userIdClaim = User.FindFirst("AccountId");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int managerId))
                {
                    var failResponse = ApiResponse<PagedResult<MaintenanceResponseDto>>.Fail(401, "Phiên đăng nhập không hợp lệ hoặc không xác định được danh tính Quản lý.");
                    return Unauthorized(failResponse);
                }
                var result = await _maintenanceService.GetRequestsForManagerAsync(managerId, pageIndex, pageSize);
                var successResponse = ApiResponse<PagedResult<MaintenanceResponseDto>>.Success(result, "Lấy danh sách yêu cầu bảo trì thành công.");
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<PagedResult<MaintenanceResponseDto>>.Fail(500, "Đã xảy ra lỗi trong quá trình xử lý: " + ex.Message);
                return StatusCode(500, errorResponse);
            }
        }


    }
}