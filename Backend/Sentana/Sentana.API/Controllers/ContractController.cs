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
                    message = "Mã hợp đồng không hợp lệ."
                });
            }

            if (request == null)
            {
                return BadRequest(new
                {
                    message = "Nội dung yêu cầu là bắt buộc."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _contractService.TerminateContractAsync(id, request);

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}/extend")]
        public async Task<IActionResult> ExtendContract(int id, [FromBody] ExtendContractDto request)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Mã hợp đồng không hợp lệ." });
            }

            if (request == null)
            {
                return BadRequest(new { message = "Dữ liệu gửi lên không được để trống." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _contractService.ExtendContractAsync(id, request);

            return StatusCode(result.StatusCode, result);
        }
        [HttpPost]
        public async Task<IActionResult> CreateContract([FromBody] CreateContractDto request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    message = "Request body không được để trống."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _contractService.CreateContractAsync(request);

            return StatusCode(result.StatusCode, result);
        }
    }
}