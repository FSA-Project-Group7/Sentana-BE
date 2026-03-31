using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Sentana.API.Services.SStorage
{
    public interface IMinioService
    {
        Task<string> UploadContractAsync(IFormFile file, int contractId, decimal version);

        Task<string> UploadFileAsync(IFormFile file, string folderName);
    }
}