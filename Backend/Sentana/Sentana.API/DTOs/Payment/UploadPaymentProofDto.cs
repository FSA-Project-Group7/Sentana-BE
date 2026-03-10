using Microsoft.AspNetCore.Http;

namespace Sentana.API.DTOs.Payment
{
    public class UploadPaymentProofDto
    {
        public IFormFile? File { get; set; }

        public string? Note { get; set; }
    }
}