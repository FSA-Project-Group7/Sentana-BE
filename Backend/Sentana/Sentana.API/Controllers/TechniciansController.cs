using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Technician;
using Sentana.API.Helpers;
using Sentana.API.Services.STechnician;
using System.Security.Claims;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Manager")]
    public class TechniciansController : ControllerBase
    {
        private readonly ITechnicianService _technicianService;

        public TechniciansController(ITechnicianService technicianService)
        {
            _technicianService = technicianService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTechnician()
        {
            try
            {
                var technicians = await _technicianService.GetAllTechnician();
                var response = ApiResponse<IEnumerable<TechnicianResponseDto>>.Success(technicians);
                return Ok(response);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<IEnumerable<TechnicianResponseDto>>.Fail(500, $"Đã xảy ra lỗi khi lấy danh sách kĩ thuật viên: {ex.Message}");
                return StatusCode(500, errorResponse);
            }
        }

        [HttpPost("CreateTechnician")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateTechnician([FromBody] CreateTechnicianRequestDto technicianRequest)
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
                return Unauthorized(ApiResponse<object>.Fail(401, "Không thể xác định danh tính người quản lý. Vui lòng đăng nhập lại."));
            }
            try
            {
                var newTechnician = await _technicianService.CreateTechnician(technicianRequest, managerId);
                return Ok(ApiResponse<TechnicianResponseDto>.Success(newTechnician, "Tạo tài khoản kĩ thuật viên thành công!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, $"Tạo tài khoản Kỹ thuật viên thất bại.{ex.Message}"));
            }
        }

        [HttpPut("UpdateTechnician/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> UpdateTechnician(int id, [FromBody] UpdateTechnicianRequestDto technicianRequest)
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
                return Unauthorized(ApiResponse<object>.Fail(401, "Không thể xác định danh tính người quản lý. Vui lòng đăng nhập lại."));
            }
            try
            {
                var updatedTechnician = await _technicianService.UpdateTechnician(id, technicianRequest, managerId);
                return Ok(ApiResponse<TechnicianResponseDto>.Success(updatedTechnician, "Cập nhật tài khoản Kỹ thuật viên thành công!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, $"Cập nhật tài khoản kĩ thuật viên thất bại. {ex.Message}"));
            }
        }

        [HttpPut("toggleStatus/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ToggleTechnicianStatus(int id)
        {
            try
            {
                string message = await _technicianService.ToggleTechnicianStatus(id);
                return Ok(ApiResponse<object>.Success(null, message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
            }
        }

		[HttpPut("toggleAvailability/{id}")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> ToggleTechAvailability(int id)
		{
			try
			{
				string message = await _technicianService.ToggleTechAvailability(id);
				return Ok(ApiResponse<object>.Success(null, message));
			}
			catch (Exception ex)
			{
				return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
			}
		}

		[HttpDelete("DeleteTechnician/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteTechnician(int id)
        {
            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Phiên đăng nhập không hợp lệ."));
            }
            try
            {
                var result = await _technicianService.DeleteTechnician(id, managerId);
                if (result)
                {
                    return Ok(ApiResponse<object>.Success(null, "Đã xóa kỹ thuật viên thành công."));
                }
                return BadRequest(ApiResponse<object>.Fail(400, "Xóa kỹ thuật viên thất bại."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(400, ex.Message));
            }
        }

        [HttpGet("Deleted")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetDeletedTechnicians()
        {
            try
            {
                var technicians = await _technicianService.GetDeletedTechnicians();
                return Ok(ApiResponse<IEnumerable<TechnicianResponseDto>>.Success(technicians, "Lấy danh sách kỹ thuật viên đã xóa thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(500, $"Lỗi: {ex.Message}"));
            }
        }

        [HttpPut("Restore/{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> RestoreTechnician(int id)
        {
            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Phiên đăng nhập không hợp lệ."));
            }
            try
            {
                var result = await _technicianService.RestoreTechnician(id, managerId);
                if (result)
                {
                    return Ok(ApiResponse<object>.Success(null, "Khôi phục tài khoản kỹ thuật viên thành công!"));
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
        public async Task<IActionResult> HardDeleteTechnician(int id)
        {
            try
            {
                var result = await _technicianService.HardDeleteTechnician(id);
                if (result)
                {
                    return Ok(ApiResponse<object>.Success(null, "Đã xóa vĩnh viễn kỹ thuật viên khỏi hệ thống."));
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
