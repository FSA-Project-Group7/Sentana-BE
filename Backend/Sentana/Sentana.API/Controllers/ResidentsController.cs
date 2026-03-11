using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Sentana.API.DTOs.Resident;
using Sentana.API.DTOs.Technician;
using Sentana.API.Helpers;
using Sentana.API.Services;
using System.Security.Claims;

namespace Sentana.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Manager")]
    public class ResidentsController : ControllerBase
    {
        private readonly ResidentService     _residentService;

        public ResidentsController(ResidentService residentService)
        {
            _residentService = residentService;
        }

        [HttpPost("CreateResident")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateResident([FromBody] CreateResidentRequestDto residentRequest)
        {
            if (!ModelState.IsValid)
            {
                string firstError = ModelState.Values
                                      .SelectMany(v => v.Errors)
                                      .Select(e => e.ErrorMessage)
                                      .FirstOrDefault() ?? "Dữ liệu đầu vào không hợp lệ.";
                return BadRequest(ApiResponse<object>.Fail(400, firstError));
            }
            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Không thể xác định danh tính Manager. Vui lòng đăng nhập lại."));
            }

            try
            {
                var newResident = await _residentService.CreateResident(residentRequest, managerId);
                return Ok(ApiResponse<ResidentResponseDto>.Success(newResident, "Tạo tài khoản Cư dân thành công!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
            }
        }

        [HttpGet("GetAllResidents")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetAllResidents()
        {
            try
            {
                var residents = await _residentService.GetAllResidents();
                var response = ApiResponse<IEnumerable<ResidentResponseDto>>.Success(residents);
                return Ok(response);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<IEnumerable<ResidentResponseDto>>.Fail(500, $"Đã xảy ra lỗi khi lấy danh sách cư dân: {ex.Message}");
                return StatusCode(500, errorResponse);
            }
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignResident(AssignResidentRequestDto request)
        {
            var result = await _residentService.AssignResident(request);

            if (!result)
                return BadRequest("Resident not found");

            return Ok(result);
        }

        // US41 - Import Resident List from Excel
        [HttpPost("import")]
        [Authorize(Roles = "Manager")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportResidents([FromForm] ImportResidentsRequestDto request)
        {
            var file = request?.File;
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Vui lòng upload file Excel (.xlsx)."));
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Hệ thống chỉ chấp nhận file định dạng .xlsx."));
            }

            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Không thể xác định danh tính Manager. Vui lòng đăng nhập lại."));
            }

            using var stream = file.OpenReadStream();
            var result = await _residentService.ImportResidentsFromExcelAsync(stream, managerId);

            return Ok(ApiResponse<ImportResidentsResultDto>.Success(result, "Import danh sách cư dân hoàn tất."));
        }
    }
}