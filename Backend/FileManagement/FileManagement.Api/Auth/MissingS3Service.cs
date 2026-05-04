using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;

namespace FileManagement.Api.Auth
{
    public class MissingS3Service : IAWSS3Service
    {
        private static Exception Missing() =>
            new InvalidOperationException("AWS S3 is not configured. Set AWSS3:AccessKeyId, AWSS3:SecretAccessKey, AWSS3:Region, AWSS3:BucketName.");

        public Task<(string Url, string Key)> UploadFileAsync(UploadFileRequest file, string? bucketName = null) => throw Missing();

        public Task<bool> DeleteFileAsync(string fileKey, string? bucketName = null) => throw Missing();

        public Task<bool> DeleteRangeAsync(string[] fileKeys, string? bucketName = null) => throw Missing();

        public Task<string> CreateUploadUrlAsync(string fileKey, string contentType, string? bucketName = null, int expirationMinutes = 15) => throw Missing();

        public Task<string> GetFileUrlAsync(string fileKey, string? bucketName = null, int expirationMinutes = 60, string? responseContentDisposition = null, string? responseContentType = null) => throw Missing();
    }
}

