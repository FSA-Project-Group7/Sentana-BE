using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Payment;
using Sentana.API.Services;

namespace Sentana.API.Controllers
{
    [Route("api/payment")]
    [ApiController]
    [Authorize]
    public class PaymentController(IPaymentService paymentService) : ControllerBase
    {
        [HttpPost("{invoiceId}/upload-proof")]
        public async Task<IActionResult> UploadPaymentProof(int invoiceId, [FromForm] UploadPaymentProofDto request)
        {
            var result = await paymentService.UploadPaymentProofAsync(invoiceId, request);

            return StatusCode(result.StatusCode, result);
        }
    }
}