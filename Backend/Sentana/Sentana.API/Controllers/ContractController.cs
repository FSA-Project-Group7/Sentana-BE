using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sentana.API.DTOs.Contracts;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ContractController(IContractService contractService) : ControllerBase
    {
        private readonly IContractService _contractService = contractService;

        [HttpPost("{id}/terminate")]
        public async Task<IActionResult> TerminateContract(int id, [FromBody] TerminateContractDto request)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Invalid contract ID."
                });
            }

            if (request == null)
            {
                return BadRequest(new
                {
                    message = "Request body is required."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _contractService.TerminateContractAsync(id, request);

            return StatusCode(result.StatusCode, result);
        }
    }
}