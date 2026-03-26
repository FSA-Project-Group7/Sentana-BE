using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Sentana.API.DTOs.Contracts;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/contract")]
    [ApiController]
    [Authorize]
    public class ContractController : ControllerBase
    {
        private readonly IContractService _contractService;

        public ContractController(IContractService contractService)
        {
            _contractService = contractService;
        }

        [Authorize(Roles = "Manager")]
        [HttpPut("{id}/terminate")]
        public async Task<IActionResult> Terminate(int id, [FromBody] TerminateContractDto dto)
        {
            var result = await _contractService.TerminateContractAsync(id, dto);
            return Ok(result);
        }

        [Authorize(Roles = "Manager")]
        [HttpPut("{id}/extend")]
        public async Task<IActionResult> ExtendContract(int id, [FromBody] ExtendContractDto request)
        {
            var result = await _contractService.ExtendContractAsync(id, request);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpPost("create-contract")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateContract([FromForm] CreateContractDto request)
        {
            var accountIdClaim = User.FindFirst("AccountId");
            if (accountIdClaim == null)
                return Unauthorized("Token không hợp lệ");

            int accountId = int.Parse(accountIdClaim.Value);

            var result = await _contractService.CreateContractAsync(request, accountId);

            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpPut("{id}/update-contract")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateContract(int id, [FromForm] UpdateContractDto request)
        {
            var result = await _contractService.UpdateContractAsync(id, request);
            return StatusCode(result.StatusCode, result);
        }


        [HttpGet("view-contract/{id}")]
        public async Task<IActionResult> GetContractDetail(int id)
        {
            var result = await _contractService.GetContractDetailAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Resident")]
        [HttpGet("my-contract")]
        public async Task<IActionResult> GetMyContract()
        {
            var accountIdClaim = User.FindFirst("AccountId");
            if (accountIdClaim == null)
                return Unauthorized("Token không hợp lệ");

            int accountId = int.Parse(accountIdClaim.Value);

            var result = await _contractService.GetMyContractAsync(accountId);

            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("view-all-contract")]
        public async Task<IActionResult> GetContractList()
        {
            var result = await _contractService.GetContractListAsync();
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("deleted-contracts")]
        public async Task<IActionResult> GetDeletedContracts()
        {
            var result = await _contractService.GetDeletedContractsAsync();
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpDelete("{id}/soft-delete")]
        public async Task<IActionResult> SoftDeleteContract(int id)
        {
            var result = await _contractService.SoftDeleteContractAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpPut("{id}/restore")]
        public async Task<IActionResult> RestoreContract(int id)
        {
            var result = await _contractService.RestoreContractAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager")]
        [HttpDelete("{id}/hard-delete")]
        public async Task<IActionResult> HardDeleteContract(int id)
        {
            var result = await _contractService.HardDeleteContractAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}