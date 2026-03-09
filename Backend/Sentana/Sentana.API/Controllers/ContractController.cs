using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sentana.API.Services;
using Sentana.API.DTOs.Contracts;

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

            try
            {
                var result = await _contractService.TerminateContractAsync(id, request);

                if (result.StatusCode != 200)
                {
                    return StatusCode(result.StatusCode, new
                    {
                        message = result.Message
                    });
                }

                return Ok(new
                {
                    message = "Contract terminated successfully.",
                    data = result.Data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }
    }
}