using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        public async Task<IActionResult> CreateBuilding([FromBody] Building newBuilding)
        {
            try
            {
                var accountIdClaim = User.FindFirst("AccountId");
                int? accountId = null;
                if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
                {
                    accountId = parsedAccountId;
                }

                var createdBuilding = await _buildingService.CreateBuildingAsync(newBuilding, accountId);

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

        // View Building List - để kiểm tra building mới tạo
        [HttpGet]
        public async Task<IActionResult> GetBuildingList()
        {
            var buildings = await _context.Buildings
                .Where(b => b.IsDeleted == false)
                .Select(b => new
                {
                    b.BuildingId,
                    b.BuildingName,
                    b.BuildingCode,
                    b.Address,
                    b.City,
                    b.FloorNumber,
                    b.ApartmentNumber,
                    b.Status,
                    b.CreatedAt,
                    b.CreatedBy
                })
                .ToListAsync();

            return Ok(buildings);
        }
    }
}
