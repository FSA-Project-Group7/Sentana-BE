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
        private readonly ResidentService   _residentService;

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

        [HttpPut("UpdateResident/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> UpdateResident(int id, [FromBody] UpdateResidentRequestDto residentRequest)
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
                return Unauthorized(ApiResponse<object>.Fail(401, "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại."));
            }
            try
            {
                var updatedResident = await _residentService.UpdateResident(id, residentRequest, managerId);
                return Ok(ApiResponse<ResidentResponseDto>.Success(updatedResident, "Cập nhật thông tin cư dân thành công!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, $"Cập nhật tài khoản cư dân thất bại. {ex.Message}"));
            }
        }


                
        // Updated: assign resident to room using service method that requires managerId
        [HttpPost("assign")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> AssignResident([FromBody] AssignResidentRequestDto request)
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

            var (isSuccess, message) = await _residentService.AssignResidentToRoomAsync(request, managerId);

            if (!isSuccess)
                return BadRequest(ApiResponse<object>.Fail(400, message));

            return Ok(ApiResponse<object>.Success(null, message));
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

        [HttpPost("remove")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> RemoveResident([FromBody] RemoveResidentRequestDto request)
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

            var dto = await _residentService.RemoveResidentFromRoomAsync(
                new AssignResidentRequestDto { AccountId = request.AccountId, ApartmentId = request.ApartmentId, RelationshipId = request.RelationshipId },
                managerId);

            if (!dto.IsSuccess)
                return BadRequest(ApiResponse<RemoveResidentResponseDto>.Fail(400, dto.Message));

            return Ok(ApiResponse<RemoveResidentResponseDto>.Success(dto, dto.Message));
        }

        [HttpPut("toggleStatus/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ToggleResidentStatus(int id)
        {
            try
            {
                string message = await _residentService.ToggleResidentStatus(id);
                return Ok(ApiResponse<object>.Success(null, message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
            }
        }

        [HttpDelete("DeleteResident/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteResident(int id)
        {
            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Phiên đăng nhập không hợp lệ."));
            }
            try
            {
                var result = await _residentService.DeleteResident(id, managerId);
                if (result)
                {
                    return Ok(ApiResponse<object>.Success(null, "Đã xóa cư dân thành công."));
                }
                return BadRequest(ApiResponse<object>.Fail(400, "Xóa cư dân thất bại."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
            }
        }

        [HttpGet("Deleted")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetDeletedResidents()
        {
            try
            {
                var residents = await _residentService.GetDeletedResidents();
                return Ok(ApiResponse<IEnumerable<ResidentResponseDto>>.Success(residents, "Lấy danh sách cư dân đã xóa thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"Lỗi: {ex.Message}"));
            }
        }

        [HttpPut("Restore/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> RestoreResident(int id)
        {
            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Phiên đăng nhập không hợp lệ."));
            }
            try
            {
                var result = await _residentService.RestoreResident(id, managerId);
                if (result)
                {
                    return Ok(ApiResponse<object>.Success(null, "Khôi phục tài khoản cư dân thành công!"));
                }
                return BadRequest(ApiResponse<object>.Fail(400, "Khôi phục tài khoản thất bại."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
            }
        }

        [HttpDelete("HardDelete/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> HardDeleteResident(int id)
        {
            try
            {
                var result = await _residentService.HardDeleteResident(id);
                if (result)
                {
                    return Ok(ApiResponse<object>.Success(null, "Đã xóa vĩnh viễn cư dân khỏi hệ thống."));
                }
                return BadRequest(ApiResponse<object>.Fail(400, "Xóa vĩnh viễn thất bại."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
            }
        }

    }
}