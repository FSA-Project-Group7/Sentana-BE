using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BuildingsController : ControllerBase
    {
        private readonly SentanaContext _context;

        public BuildingsController(SentanaContext context)
        {
            _context = context;
        }

        // US28 - Create Building
        [HttpPost]
        public async Task<IActionResult> CreateBuilding([FromBody] Building newBuilding)
        {
            if (newBuilding == null || string.IsNullOrWhiteSpace(newBuilding.BuildingName))
            {
                return BadRequest(new { message = "Tên tòa nhà là bắt buộc." });
            }

            var isNameExists = await _context.Buildings
                .AnyAsync(b => b.BuildingName == newBuilding.BuildingName && b.IsDeleted == false);

            if (isNameExists)
            {
                return BadRequest(new { message = "Tên tòa nhà đã tồn tại." });
            }

            // Gán các giá trị hệ thống
            newBuilding.CreatedAt = DateTime.Now;
            newBuilding.IsDeleted = false;

            var accountIdClaim = User.FindFirst("AccountId");
            if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var accountId))
            {
                newBuilding.CreatedBy = accountId;
            }

            _context.Buildings.Add(newBuilding);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBuildingList), new { id = newBuilding.BuildingId }, newBuilding);
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
