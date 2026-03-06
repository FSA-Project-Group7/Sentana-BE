using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Technician;
using Sentana.API.Helpers;
using Sentana.API.Services;

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
                var allErrors = ModelState.Values
                                  .SelectMany(v => v.Errors)
                                  .Select(e => e.ErrorMessage);
                string errorMessage = string.Join(" | ", allErrors);
                return BadRequest(ApiResponse<object>.Fail(400, errorMessage));
            }
            try
            {
                var newTechnician = await _technicianService.CreateTechnician(technicianRequest);
                return Ok(ApiResponse<TechnicianResponseDto>.Success(newTechnician, "Tạo tài khoản Kỹ thuật viên thành công!"));
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
                var allErrors = ModelState.Values
                                  .SelectMany(v => v.Errors)
                                  .Select(e => e.ErrorMessage);
                string errorMessage = string.Join(" | ", allErrors);
                return BadRequest(ApiResponse<object>.Fail(400, errorMessage));
            }
            try
            {
                var updatedTechnician = await _technicianService.UpdateTechnician(id, technicianRequest);
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
    }
}
