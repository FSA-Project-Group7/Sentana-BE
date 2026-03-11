using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Utility;
using Sentana.API.Helpers; 
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UtilityController : BaseController
    {
        private readonly IUtilityService _utilityService;

        public UtilityController(IUtilityService utilityService)
        {
            _utilityService = utilityService;
        }

        // Chỉ manager được nhập số điện nước
        [HttpPost("electric/input")]
        [Authorize(Roles = "Manager")] 
        public async Task<IActionResult> InputElectricIndex([FromBody] InputElectricIndexDto request)
        {
            int currentUserId = GetCurrentAccountId();
            var result = await _utilityService.InputElectricityIndexAsync(request, currentUserId);

            if (!result.IsSuccess)
            {
                // Nếu lỗi do không tìm thấy phòng -> Trả về 404
                if (result.Message.Contains("Không tìm thấy") || result.Message.Contains("not found"))
                {
                    return NotFound(ApiResponse<string>.Fail(404, result.Message));
                }

                // Các lỗi logic khác (Ngày tương lai, Chỉ số nhỏ...) -> Trả về 400
                return BadRequest(ApiResponse<string>.Fail(400, result.Message));
            }

            return Ok(ApiResponse<string>.Success(null, result.Message));
        }

        [HttpPost("water/input")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> InputWaterIndex([FromBody] InputWaterIndexDto request)
        {
            int currentUserId = GetCurrentAccountId();
            var result = await _utilityService.InputWaterIndexAsync(request, currentUserId);

            if (!result.IsSuccess) return BadRequest(ApiResponse<string>.Fail(400, result.Message));

            return Ok(ApiResponse<string>.Success(null, result.Message));
        }

        // Dành cho Cư dân (Tự động móc ID phòng từ Token)
        [HttpGet("history/my")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> GetMyUtilityHistory([FromQuery] int? month, [FromQuery] int? year)
        {
            var result = await _utilityService.GetUtilityHistoryAsync(User, null, month, year);
            if (!result.IsSuccess) return BadRequest(ApiResponse<string>.Fail(400, result.Message));

            return Ok(ApiResponse<List<UtilityHistoryDto>>.Success(result.Data, "Lấy lịch sử thành công."));
        }

        // Dành cho Quản lý (Bắt buộc truyền ID phòng)
        [HttpGet("history/apartment/{apartmentId}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetUtilityHistoryByApartment(int apartmentId, [FromQuery] int? month, [FromQuery] int? year)
        {
            var result = await _utilityService.GetUtilityHistoryAsync(User, apartmentId, month, year);
            if (!result.IsSuccess) return BadRequest(ApiResponse<string>.Fail(400, result.Message));

            return Ok(ApiResponse<List<UtilityHistoryDto>>.Success(result.Data, "Lấy lịch sử thành công."));
        }

        //Import chỉ số Điện/Nước bằng file Excel
        [HttpPost("import")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ImportExcel(IFormFile file, [FromQuery] string type)
        {
            if (type != "electric" && type != "water")
                return BadRequest(ApiResponse<string>.Fail(400, "Loại tiện ích (type) phải là 'electric' hoặc 'water'."));

            var result = await _utilityService.ImportUtilityExcelAsync(file, type, 1);
            if (!result.IsSuccess) return BadRequest(ApiResponse<string>.Fail(400, result.Message));

            return Ok(ApiResponse<string>.Success(null, result.Message));
        }
    }
}