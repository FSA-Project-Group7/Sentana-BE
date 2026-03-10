using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Invoice;
using Sentana.API.Helpers;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {   
            _paymentService = paymentService;
        }

        // US13 - View Payment History
        [HttpGet("/api/invoices/history")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> GetPaymentHistory([FromQuery] int? apartmentId = null, [FromQuery] int? accountId = null)
        {
            try
            {
                var history = await _paymentService.GetPaymentHistoryAsync(User, apartmentId, accountId);
                return Ok(ApiResponse<List<PaymentHistoryItemDto>>.Success(history, "Lấy lịch sử thanh toán thành công."));
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
    }
}
