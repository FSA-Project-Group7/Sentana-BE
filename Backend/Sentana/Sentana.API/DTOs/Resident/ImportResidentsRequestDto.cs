using Microsoft.AspNetCore.Http;

namespace Sentana.API.DTOs.Resident
{
    public class ImportResidentsRequestDto
    {
        public IFormFile File { get; set; } = null!;
    }
}
