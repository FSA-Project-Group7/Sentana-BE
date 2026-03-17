using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
                return BadRequest(new { message = "Mã hợp đồng không hợp lệ." });

            if (request == null)
                return BadRequest(new { message = "Nội dung yêu cầu là bắt buộc." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _contractService.TerminateContractAsync(id, request);

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}/extend")]
        public async Task<IActionResult> ExtendContract(int id, [FromBody] ExtendContractDto request)
        {
            if (id <= 0)
                return BadRequest(new { message = "Mã hợp đồng không hợp lệ." });

            if (request == null)
                return BadRequest(new { message = "Dữ liệu gửi lên không được để trống." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _contractService.ExtendContractAsync(id, request);

            return StatusCode(result.StatusCode, result);
        }

        // 🔐 Chỉ Manager và Technician được tạo contract
        [Authorize(Roles = "Manager,Technician")]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateContract([FromForm] CreateContractDto request)
        {
            if (request == null)
                return BadRequest(new { message = "Request body không được để trống." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.ApartmentId <= 0)
                return BadRequest(new { message = "ApartmentId không hợp lệ." });

            if (request.EndDay <= request.StartDay)
                return BadRequest(new { message = "Ngày kết thúc phải lớn hơn ngày bắt đầu." });

            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { message = "File hợp đồng là bắt buộc." });

            // 🔹 Lấy AccountId từ token
            var accountIdClaim = User.FindFirst("AccountId") ??
                                 User.FindFirst(ClaimTypes.NameIdentifier);

            if (accountIdClaim == null)
                return Unauthorized(new { message = "Không xác định được người dùng." });

            int accountId = int.Parse(accountIdClaim.Value);

            var result = await _contractService.CreateContractAsync(request, accountId);

            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Manager,Technician")]
        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateContract(int id, [FromForm] UpdateContractDto request)
        {
            if (id <= 0)
                return BadRequest(new { message = "Mã hợp đồng không hợp lệ." });

            if (request == null)
                return BadRequest(new { message = "Request body không được để trống." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (request.StartDay.HasValue && request.EndDay.HasValue)
            {
                if (request.EndDay <= request.StartDay)
                    return BadRequest(new { message = "Ngày kết thúc phải lớn hơn ngày bắt đầu." });
            }

            if (request.File != null && request.File.Length == 0)
                return BadRequest(new { message = "File upload không hợp lệ." });

            var result = await _contractService.UpdateContractAsync(id, request);

            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetContractDetail(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "Mã hợp đồng không hợp lệ." });

            var result = await _contractService.GetContractDetailAsync(id);

            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        public async Task<IActionResult> GetContractList()
        {
            var result = await _contractService.GetContractListAsync();

            return StatusCode(result.StatusCode, result);
        }
    }
}