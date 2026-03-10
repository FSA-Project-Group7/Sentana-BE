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

        // US12
        [HttpGet("current")]
        [Authorize]
        public async Task<IActionResult> GetCurrentInvoice([FromQuery] int? apartmentId = null, [FromQuery] int? accountId = null)
        {
            try
            {
                var dto = await _invoiceService.GetCurrentInvoiceAsync(User, apartmentId, accountId);

                if (dto == null)
                    return NotFound(ApiResponse<string>.Fail(404, "Không tìm thấy hóa đơn cho tháng hiện tại."));

                return Ok(ApiResponse<InvoiceResponseDto>.Success(dto, "Lấy hóa đơn thành công."));
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