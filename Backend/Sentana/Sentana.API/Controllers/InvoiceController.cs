using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Common;
using Sentana.API.DTOs.Invoice;
using Sentana.API.DTOs.Payment;
using Sentana.API.Helpers;
using Sentana.API.Services.SInvoice;

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

        // us12
        [HttpGet("my-invoices")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> GetMyInvoices([FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            try
            {
                // Truyền null cho cả apartmentId và accountId. Service sẽ tự bóc Token lấy tài khoản.
                var dtos = await _invoiceService.GetCurrentInvoicesAsync(User, month, year, null, null);

                if (dtos == null || !dtos.Any())
                    return NotFound(ApiResponse<string>.Fail(404, "Bạn không có hóa đơn nào trong khoảng thời gian này."));

                return Ok(ApiResponse<List<InvoiceResponseDto>>.Success(dtos, "Lấy hóa đơn thành công."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail(400, ex.Message));
            }
        }

       // us68
        [HttpGet("apartment/{apartmentId}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetInvoiceByApartment(int apartmentId, [FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            try
            {
                var dtos = await _invoiceService.GetCurrentInvoicesAsync(User, month, year, apartmentId, null);

                if (dtos == null || !dtos.Any())
                    return NotFound(ApiResponse<string>.Fail(404, $"Không tìm thấy hóa đơn cho căn hộ ID: {apartmentId}."));

                return Ok(ApiResponse<List<InvoiceResponseDto>>.Success(dtos, "Lấy hóa đơn thành công."));
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
            {
                if (result.Message.Contains("Không tìm thấy") || result.Message.Contains("không tồn tại"))
                {
                    return NotFound(ApiResponse<string>.Fail(404, result.Message));
                }

                return BadRequest(ApiResponse<string>.Fail(400, result.Message));
            }

            return Ok(ApiResponse<string>.Success(null, result.Message));
        }

        // Danh sách Hóa đơn tổng quan cho Quản lý 
        [HttpGet("list")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetInvoiceList([FromQuery] InvoiceListRequestDto request)
        {
            try
            {
                var result = await _invoiceService.GetInvoiceListAsync(request);
                return Ok(ApiResponse<PagedResult<InvoiceListItemDto>>.Success(result, "Lấy danh sách thành công."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Fail(400, ex.Message));
            }
        }

        // Edit Invoice
        [HttpPut("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> EditInvoice(int id, [FromBody] EditInvoiceDto request)
        {
            var result = await _invoiceService.EditInvoiceAsync(id, request);
            if (!result.IsSuccess)
            {
                if (result.Message.Contains("Không tìm thấy")) return NotFound(ApiResponse<string>.Fail(404, result.Message));
                return BadRequest(ApiResponse<string>.Fail(400, result.Message));
            }
            return Ok(ApiResponse<string>.Success(null, result.Message));
        }
        // Approve payment
        [HttpPut("transaction/{id}/approve")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> ApprovePayment(int id)
    {
        // Lấy ID người dùng từ Token (ClaimType NameIdentifier thường dùng lưu ID)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(ApiResponse<string>.Fail(401, "Không xác định được danh tính người dùng."));
        }

        // Gọi Service với UserId thực tế
        var result = await _invoiceService.ApprovePaymentAsync(id, currentUserId);

        if (!result.IsSuccess)
        {
            if (result.Message.Contains("Không tìm thấy")) return NotFound(ApiResponse<string>.Fail(404, result.Message));
            return BadRequest(ApiResponse<string>.Fail(400, result.Message));
        }
        return Ok(ApiResponse<string>.Success(null, result.Message));
    }

    // Reject Payment
    [HttpPut("transaction/{id}/reject")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> RejectPayment(int id, [FromBody] RejectPaymentDto request)
    {
        // Lấy ID người dùng từ Token
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
        {
            return Unauthorized(ApiResponse<string>.Fail(401, "Không xác định được danh tính người dùng."));
        }

        // Gọi Service với UserId thực tế
        var result = await _invoiceService.RejectPaymentAsync(id, request, currentUserId);

        if (!result.IsSuccess)
        {
            if (result.Message.Contains("Không tìm thấy")) return NotFound(ApiResponse<string>.Fail(404, result.Message));
            return BadRequest(ApiResponse<string>.Fail(400, result.Message));
        }
        return Ok(ApiResponse<string>.Success(null, result.Message));
    }
}
}