using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentana.API.DTOs.Invoice;
using Sentana.API.Models;
using Sentana.API.Services;
using System.Security.Claims;

namespace Sentana.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoiceController(SentanaContext context)
        {
            _invoiceService = new InvoiceService(context);
        }
        /// <param name="apartmentId">optional for Manager</param>
        /// <param name="accountId">optional for Manager</param>
        [HttpGet("current")]
        [Authorize] // Resident or Manager must be authenticated
        [ProducesResponseType(typeof(InvoiceResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCurrentInvoice([FromQuery] int? apartmentId = null, [FromQuery] int? accountId = null)
        {
            try
            {
                var dto = await _invoiceService.GetCurrentInvoiceAsync(User, apartmentId, accountId);
                if (dto == null)
                    return NotFound(new { message = "Không tìm thấy hóa đơn cho tháng hiện tại." });

                return Ok(dto);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Invalid token or AccountId claim missing." });
            }
        }
    }
}
