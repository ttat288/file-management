using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileManagement.Data.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryService> _logger;

        public CloudinaryService(Cloudinary cloudinary, ILogger<CloudinaryService> logger)
        {
            _cloudinary = cloudinary;
            _logger = logger;
        }

        public async Task<(string Url, string PublicId)> UploadFileAsync(UploadFileRequest file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            const long maxSize = 10L * 1024 * 1024 * 1024; // 10GB
            if (file.Length > maxSize)
                throw new ArgumentException("File size exceeds 10GB limit", nameof(file));

            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, file.Content),
                Folder = "file-management",
                PublicIdPrefix = Guid.NewGuid().ToString("N")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
            {
                _logger.LogError($"Cloudinary upload error: {uploadResult.Error.Message}");
                throw new InvalidOperationException($"Upload failed: {uploadResult.Error.Message}");
            }

            _logger.LogInformation($"File uploaded successfully. Public ID: {uploadResult.PublicId}");
            return (uploadResult.SecureUrl.ToString(), uploadResult.PublicId);
        }

        public async Task<bool> DeleteFileAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId) { ResourceType = ResourceType.Auto };
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Result == "ok")
            {
                _logger.LogInformation($"File deleted successfully. Public ID: {publicId}");
                return true;
            }

            _logger.LogWarning($"Failed to delete file. Public ID: {publicId}");
            return false;
        }

        public async Task<string> GetFileUrlAsync(string publicId, int expirationMinutes = 60)
        {
            var result = await _cloudinary.GetResourceAsync(new GetResourceParams(publicId) { ResourceType = ResourceType.Auto });
            if (result.Error != null)
                throw new InvalidOperationException($"Failed to get file URL: {result.Error.Message}");

            return result.SecureUrl;
        }
    }
}
