using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Info;
using Sentana.API.Helpers;
using Sentana.API.Services.SInfo;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Manager")]
    public class InFosController : ControllerBase
    {
        private readonly IInfoService _infoService;

        public InFosController(IInfoService infoService)
        {
            _infoService = infoService;
        }
        [HttpGet("check-cccd/{cccd}")]
        public async Task<IActionResult> CheckCccd(string cccd)
        {
            if (string.IsNullOrEmpty(cccd) || !System.Text.RegularExpressions.Regex.IsMatch(cccd, ValidationHelper.CccdRegex))
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Định dạng CCCD không hợp lệ phải đủ 12 chữ số."));
            }
            try
            {
                var result = await _infoService.GetInfoByCccd(cccd);
                if (result == null)
                { 
                    return NotFound(ApiResponse<object>.Fail(404, "Hồ sơ chưa tồn tại."));
                }
                return Ok(ApiResponse<InfoCheckResponseDto>.Success(result, "Đã tìm thấy hồ sơ sẵn có."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"Lỗi hệ thống: {ex.Message}"));
            }
        }
    }


}
