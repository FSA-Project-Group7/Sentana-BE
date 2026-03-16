using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Payment;
using Sentana.API.Services.SPayment;

namespace Sentana.API.Controllers
{
    [Route("api/payment")]
    [ApiController]
    [Authorize]
    public class PaymentController(IPaymentService paymentService) : ControllerBase
    {
        private readonly IPaymentService _paymentService = paymentService;

        // Upload payment proof
        [HttpPost("{invoiceId}/upload-proof")]
        public async Task<IActionResult> UploadPaymentProof(int invoiceId, [FromForm] UploadPaymentProofDto request)
        {
            if (invoiceId <= 0)
            {
                return BadRequest(new
                {
                    message = "Invoice ID không hợp lệ."
                });
            }

            var result = await _paymentService.UploadPaymentProofAsync(invoiceId, request);

            return StatusCode(result.StatusCode, result);
        }

        // Lấy danh sách payment theo invoice
        [HttpGet("invoice/{invoiceId}")]
        public async Task<IActionResult> GetPaymentsByInvoice(int invoiceId)
        {
            if (invoiceId <= 0)
            {
                return BadRequest(new
                {
                    message = "Invoice ID không hợp lệ."
                });
            }

            var result = await _paymentService.GetPaymentsByInvoiceAsync(invoiceId);

            return StatusCode(result.StatusCode, result);
        }

        // Lấy chi tiết transaction
        [HttpGet("transaction/{transactionId}")]
        public async Task<IActionResult> GetPaymentDetail(int transactionId)
        {
            if (transactionId <= 0)
            {
                return BadRequest(new
                {
                    message = "Transaction ID không hợp lệ."
                });
            }

            var result = await _paymentService.GetPaymentDetailAsync(transactionId);

            return StatusCode(result.StatusCode, result);
        }

        // Lấy lịch sử payment của resident
        [HttpGet("history")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var result = await _paymentService.GetPaymentHistoryAsync(User);
            return StatusCode(result.StatusCode, result);
        }
    }
}