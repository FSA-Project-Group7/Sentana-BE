using Microsoft.AspNetCore.Http;

namespace Sentana.API.Services.SStorage
{
    public interface IMinioService
    {
        Task<string> UploadFileAsync(IFormFile file, string folder);
    }
}