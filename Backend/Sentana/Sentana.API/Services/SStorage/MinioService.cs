using Minio;
using Minio.DataModel.Args;

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

        public async Task<string> UploadFileAsync(IFormFile file, string folder)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);

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
    }
}