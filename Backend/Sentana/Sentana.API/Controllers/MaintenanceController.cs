using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Maintenance;
using Sentana.API.Services.SMaintenance; // Ensure this matches your namespace
using System.Security.Claims;

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

        // US22 & US23
        [HttpGet("assigned-to-me")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> GetMyTasks()
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.GetMyAssignedTasksAsync(userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message, result.Data });
        }

        // US24: Accept Task
        [HttpPut("{id}/accept")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> AcceptTask(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.AcceptTaskAsync(id, userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message });
        }

        // US25: Start Processing
        [HttpPut("{id}/start")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> StartTask(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.StartProcessingTaskAsync(id, userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message });
        }

        // US26: Fix Task
        [HttpPut("{id}/fix")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> FixTask(int id, [FromBody] FixTaskRequestDto request)
        {
            var userId = GetCurrentUserId();
            var result = await _maintenanceService.FixTaskAsync(id, request, userId);
            if (!result.IsSuccess) return BadRequest(new { result.Message });
            return Ok(new { result.Message });
        }
    }
}