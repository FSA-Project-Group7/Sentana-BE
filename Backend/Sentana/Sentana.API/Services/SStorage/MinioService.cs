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

        public async Task<string> UploadImageAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File ảnh không hợp lệ.");
            const int maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
                throw new ArgumentException("Dung lượng ảnh không được vượt quá 5MB.");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            if (!Array.Exists(allowedExtensions, e => e == extension))
                throw new ArgumentException("Chỉ chấp nhận file ảnh định dạng JPG, JPEG, PNG.");
            using var stream = file.OpenReadStream();
            var headerBytes = new byte[8]; 
            await stream.ReadAsync(headerBytes, 0, 8);
            bool isValidSignature = false;
            if (headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47)
            {
                isValidSignature = true;
            }
            else if (headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF)
            {
                isValidSignature = true;
            }

            if (!isValidSignature)
            {
                throw new ArgumentException("Nội dung file bị sai lệch hoặc giả mạo định dạng. Vui lòng upload ảnh thật.");
            }
            stream.Position = 0;
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var objectName = string.IsNullOrWhiteSpace(folderName)
                ? uniqueFileName
                : $"{folderName.Trim('/')}/{uniqueFileName}";
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