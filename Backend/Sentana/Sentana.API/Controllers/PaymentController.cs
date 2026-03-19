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

        // ✅ NEW: Upload proof KHÔNG cần invoiceId
        [HttpPost("upload-proof")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> UploadPaymentProofAuto([FromForm] UploadPaymentProofDto request)
        {
            var result = await _paymentService.UploadPaymentProofAutoAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        // 🔹 GIỮ NGUYÊN API CŨ (để team khác không bị ảnh hưởng)
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

        [HttpGet("history")]
        [Authorize(Roles = "Resident")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var result = await _paymentService.GetPaymentHistoryAsync(User);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("all")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GetAllTransactions()
        {
            var result = await _paymentService.GetAllTransactionsAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}