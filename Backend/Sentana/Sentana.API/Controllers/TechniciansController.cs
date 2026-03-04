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
                var errorResponse = ApiResponse<IEnumerable<TechnicianResponseDto>>.Fail(500, $"Đã xảy ra lỗi khi lấy danh sách kĩ thuật viên: ex.Message");
                return StatusCode(500, errorResponse);
            }
        }
    }
}
