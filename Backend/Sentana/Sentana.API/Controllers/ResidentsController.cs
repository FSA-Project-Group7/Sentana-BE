using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Resident;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResidentsController : ControllerBase
    {
        private readonly ResidentService _residentService;

        public ResidentsController(ResidentService residentService)
        {
            _residentService = residentService;
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignResident(AssignResidentRequestDto request)
        {
            var result = await _residentService.AssignResident(request);

            if (!result)
                return BadRequest("Resident not found");

            return Ok(result);
        }
    }
}