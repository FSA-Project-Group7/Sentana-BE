using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Building;
using Sentana.API.Enums;
using Sentana.API.Models;
using Sentana.API.Services.SBuilding;

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
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [Authorize(Roles = "Manager,Technician")] // Thêm role Technician cách nhau bởi dấu phẩy
        [ProducesResponseType(StatusCodes.Status200OK)]
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
					Status = (byte?)b.Status
				})
                .ToListAsync();

            return Ok(buildings);
        }

		// Lấy danh sách xóa mềm
		[HttpGet("deleted")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> GetDeletedBuildings()
		{
			try
			{
				var buildings = await _buildingService.GetDeletedBuildingsAsync();
				return Ok(buildings);
			}
			catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
		}

		// Khôi phục
		[HttpPut("{id}/restore")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> RestoreBuilding(int id)
		{
			try
			{
				await _buildingService.RestoreBuildingAsync(id);
				return Ok(new { message = "Khôi phục tòa nhà thành công!" });
			}
			catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
		}

		// Xóa cứng
		[HttpDelete("{id}/hard")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> HardDeleteBuilding(int id)
		{
			try
			{
				await _buildingService.HardDeleteBuildingAsync(id);
				return Ok(new { message = "Đã xóa vĩnh viễn tòa nhà khỏi hệ thống!" });
			}
			catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
		}
	}
}
