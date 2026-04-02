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
            var userIdClaim = User.FindFirst("AccountId")?.Value;
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
        var userIdClaim = User.FindFirst("AccountId")?.Value;
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

     // Thông báo invoice
    [HttpPost("{invoiceId}/notify")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> SendInvoiceNotification(int invoiceId)
    {
        var result = await _invoiceService.SendInvoiceNotificationAsync(invoiceId);

        if (!result.IsSuccess)
        {
            if (result.Message.Contains("Không tìm thấy")) return NotFound(ApiResponse<string>.Fail(404, result.Message));
            return BadRequest(ApiResponse<string>.Fail(400, result.Message));
        }
        return Ok(ApiResponse<string>.Success(null, result.Message));
    }

        // US82 - View Outstanding Debt
        [HttpGet("outstanding-debts")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetOutstandingDebts()
        {
            try
            {
                var result = await _invoiceService.GetOutstandingDebtsAsync();

                if (result == null || !result.Any())
                    return NotFound(ApiResponse<string>.Fail(404, "Không có hóa đơn quá hạn chưa thanh toán nào."));

                return Ok(ApiResponse<List<OutstandingDebtItemDto>>.Success(result,
                    $"Tìm thấy {result.Count} hóa đơn quá hạn chưa thanh toán."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail(500, $"Lỗi hệ thống: {ex.Message}"));
            }
        }

        // US83 - Export Debt Report
        [HttpGet("export-debt")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ExportDebtReport()
        {
            try
            {
                var fileBytes = await _invoiceService.ExportDebtReportAsync();

                // Nếu file rỗng (không có nợ) thì vẫn trả về file Excel chỉ có header
                string fileName = $"Debt_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail(500, $"Lỗi xuất file Excel: {ex.Message}"));
     
            
            }
        }

        [HttpPut("{invoiceId}/status")]
        [Authorize(Roles = "Manager")] 
        public async Task<IActionResult> ChangeInvoiceStatus(int invoiceId, [FromBody] ChangeInvoiceStatusDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Lấy ID của Quản lý đang thực hiện thao tác từ Token
            var userIdClaim = User.FindFirst("AccountId")?.Value;
            if (!int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { message = "Xác thực danh tính thất bại. Vui lòng đăng nhập lại." });
            }

            var result = await _invoiceService.ChangeInvoiceStatusAsync(invoiceId, request, currentUserId);

            if (result.IsSuccess)
            {
                return Ok(new { message = result.Message });
            }

            return BadRequest(new { message = result.Message });
        }

        // US81 - View Monthly Revenue (Manager)
        [HttpGet("monthly-revenue")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int? year)
        {
            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
                return Unauthorized(ApiResponse<string>.Fail(401, "Phiên đăng nhập không hợp lệ."));

            try
            {
                var result = await _invoiceService.GetMonthlyRevenueAsync(managerId, year);
                if (result == null || !result.Any())
                    return NotFound(ApiResponse<string>.Fail(404, "Không có dữ liệu doanh thu trong năm này."));
                return Ok(ApiResponse<List<MonthlyRevenueDto>>.Success(result, "Lấy thống kê doanh thu hàng tháng thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail(500, $"Lỗi hệ thống: {ex.Message}"));
            }
        }

        // US14 - View Payment Statistics (Manager)
        [HttpGet("payment-statistics")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetPaymentStatistics([FromQuery] int? month, [FromQuery] int? year)
        {
            var managerIdStr = User.FindFirstValue("AccountId");
            if (string.IsNullOrEmpty(managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
                return Unauthorized(ApiResponse<string>.Fail(401, "Phiên đăng nhập không hợp lệ."));

            try
            {
                var result = await _invoiceService.GetPaymentStatisticsAsync(managerId, month, year);
                return Ok(ApiResponse<PaymentStatisticsDto>.Success(result, "Lấy thống kê thanh toán thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Fail(500, $"Lỗi hệ thống: {ex.Message}"));
            }
        }
    }
}
