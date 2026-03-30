using Minio;
using Minio.DataModel.Args;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sentana.API.Services.SStorage
{
    public class MinioService : IMinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName = "sentana";

        public MinioService(IMinioClient minioClient)
        {
            _minioClient = minioClient;
        }

        public async Task<string> UploadContractAsync(IFormFile file, int contractId, decimal version)
        {
            var extension = Path.GetExtension(file.FileName);
            var folder = $"contracts/contract_{contractId}";
            var fileName = $"contract_{contractId}_v{version}{extension}";
            var objectName = $"{folder}/{fileName}";

            using var stream = file.OpenReadStream();

            await _minioClient.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType)
            );

            return $"http://localhost:9000/{_bucketName}/{objectName}";
        }
        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File không hợp lệ.");

            var extension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";

            var objectName = string.IsNullOrWhiteSpace(folderName)
                ? uniqueFileName
                : $"{folderName.Trim('/')}/{uniqueFileName}";

            using var stream = file.OpenReadStream();

            await _minioClient.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType)
            );

            return $"http://localhost:9000/{_bucketName}/{objectName}";
        }
    }
}