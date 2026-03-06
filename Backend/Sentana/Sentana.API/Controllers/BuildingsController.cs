using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Sentana.API.DTOs.Building;
using Sentana.API.Models;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BuildingsController : ControllerBase
    {
        private readonly SentanaContext _context;
        private readonly IBuildingService _buildingService;

        public BuildingsController(SentanaContext context, IBuildingService buildingService)
        {
            _context = context;
            _buildingService = buildingService;
        }

        // US28 - Create Building
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateBuilding([FromBody] BuildingRequestDto newBuilding)
        {
            try
            {
                var createdBuilding = await _buildingService.CreateBuildingAsync(newBuilding, User);

                return CreatedAtAction(nameof(GetBuildingList), new { id = createdBuilding.BuildingId }, createdBuilding);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // US29 - Update Building
        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> UpdateBuilding(int id, [FromBody] BuildingRequestDto updatedBuilding)
        {
            try
            {
                var result = await _buildingService.UpdateBuildingAsync(id, updatedBuilding, User);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // US30 - Delete Building (soft delete)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            try
            {
                await _buildingService.DeleteBuildingAsync(id, User);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // View Building List - để kiểm tra building mới tạo
        [HttpGet]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetBuildingList()
        {
            var buildings = await _context.Buildings
                .Where(b => b.IsDeleted == false)
                .Select(b => new BuildingResponseDto
                {
                    BuildingId = b.BuildingId,
                    BuildingName = b.BuildingName,
                    BuildingCode = b.BuildingCode,
                    Address = b.Address,
                    City = b.City,
                    FloorNumber = b.FloorNumber,
                    ApartmentNumber = b.ApartmentNumber,
                    StatusName = b.Status.ToString() ?? string.Empty
                })
                .ToListAsync();

            return Ok(buildings);
        }
    }
}
