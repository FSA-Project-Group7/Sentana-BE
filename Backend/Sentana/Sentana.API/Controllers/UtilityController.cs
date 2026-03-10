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

        [HttpGet("history/{apartmentId}")]
        [Authorize(Roles = "Manager,Resident")] // Cư dân cũng có quyền xem lịch sử của họ
        public async Task<IActionResult> GetUtilityHistory(int apartmentId, [FromQuery] int? month, [FromQuery] int? year)
        {
            var history = await _utilityService.GetUtilityHistoryAsync(apartmentId, month, year);
            return Ok(ApiResponse<List<UtilityHistoryDto>>.Success(history, "Lấy lịch sử thành công."));
        }
    }
}