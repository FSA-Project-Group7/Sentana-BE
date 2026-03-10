using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Invoice;
using Sentana.API.Helpers;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : BaseController 
    {
        private readonly IInvoiceService _invoiceService;

        public InvoiceController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet("current")]
        [Authorize]
        public async Task<IActionResult> GetCurrentInvoice(
            [FromQuery] int? month = null,
            [FromQuery] int? year = null,
            [FromQuery] int? apartmentId = null,
            [FromQuery] int? accountId = null)
        {
            try
            {
                var dtos = await _invoiceService.GetCurrentInvoicesAsync(User, month, year, apartmentId, accountId);

                if (dtos == null || !dtos.Any())
                    return NotFound(ApiResponse<string>.Fail(404, "Không tìm thấy hóa đơn nào phù hợp."));

                return Ok(ApiResponse<List<InvoiceResponseDto>>.Success(dtos, "Lấy hóa đơn thành công."));
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(ApiResponse<string>.Fail(401, "Token không hợp lệ hoặc thiếu quyền truy cập."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail(400, ex.Message));
            }
        }

        [HttpPost("generate")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GenerateInvoices([FromBody] GenerateInvoiceRequestDto request)
        {
            int currentUserId = GetCurrentAccountId();

            var result = await _invoiceService.GenerateMonthlyInvoicesAsync(request, currentUserId);

            if (!result.IsSuccess)
                return BadRequest(ApiResponse<string>.Fail(400, result.Message));

            return Ok(ApiResponse<string>.Success(null, result.Message));
        }
    }
}