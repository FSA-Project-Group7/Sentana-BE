using Microsoft.AspNetCore.Http;

namespace Sentana.API.Services.SStorage
{
    public interface IMinioService
    {
        Task<string> UploadContractAsync(IFormFile file, int contractId, decimal version);
    }
}