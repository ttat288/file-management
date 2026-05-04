using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileManagement.Data.Services
{
    public class AzureBlobStorageService : IAzureBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<AzureBlobStorageService> _logger;
        private readonly string _containerName;

        public AzureBlobStorageService(
            BlobServiceClient blobServiceClient,
            IConfiguration configuration,
            ILogger<AzureBlobStorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
            _containerName = configuration["AzureBlobStorage:ContainerName"] ?? "file-management";
        }

        public async Task<(string Url, string Key)> UploadFileAsync(UploadFileRequest file, string? containerName = null)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            const long maxSize = 500 * 1024 * 1024; // 500MB
            if (file.Length > maxSize)
                throw new ArgumentException("File size exceeds 500MB limit", nameof(file));

            containerName ??= _containerName;

            var fileExtension = Path.GetExtension(file.FileName);
            var blobName = $"{Guid.NewGuid():N}{fileExtension}";

            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = container.GetBlobClient(blobName);
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType ?? "application/octet-stream" },
                Metadata = new Dictionary<string, string>
                {
                    { "OriginalFileName", file.FileName },
                    { "UploadedDate", DateTime.UtcNow.ToString("o") }
                }
            };

            await blobClient.UploadAsync(file.Content, uploadOptions);

            var blobUrl = blobClient.Uri.ToString();
            _logger.LogInformation($"File uploaded successfully to Azure Blob Storage. Blob Name: {blobName}, URL: {blobUrl}");

            return (blobUrl, blobName);
        }

        public async Task<bool> DeleteFileAsync(string fileKey, string containerName = null)
        {
            containerName ??= _containerName;

            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = container.GetBlobClient(fileKey);

            var result = await blobClient.DeleteIfExistsAsync();
            if (result.Value)
            {
                _logger.LogInformation($"File deleted successfully from Azure Blob Storage. Blob Name: {fileKey}");
                return true;
            }

            _logger.LogWarning($"Failed to delete file. Blob Name: {fileKey}");
            return false;
        }

        public async Task<bool> DeleteRangeAsync(string[] fileKeys, string containerName = null)
        {
            containerName ??= _containerName;
            var container = _blobServiceClient.GetBlobContainerClient(containerName);

            foreach (var fileKey in fileKeys)
            {
                var blobClient = container.GetBlobClient(fileKey);
                await blobClient.DeleteIfExistsAsync();
            }

            _logger.LogInformation($"Multiple files deleted successfully from Azure Blob Storage. Count: {fileKeys.Length}");
            return true;
        }

        public Task<string> GetFileUrlAsync(string fileKey, string containerName = null, int expirationMinutes = 60)
        {
            containerName ??= _containerName;
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = container.GetBlobClient(fileKey);

            // If container is public, this is enough. For private containers, generate SAS in a dedicated implementation.
            return Task.FromResult(blobClient.Uri.ToString());
        }
    }
}
