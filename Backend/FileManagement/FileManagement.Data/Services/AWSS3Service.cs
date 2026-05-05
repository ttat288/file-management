using Amazon.S3;
using Amazon.S3.Model;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileManagement.Data.Services
{
    public class AWSS3Service : IAWSS3Service
    {
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<AWSS3Service> _logger;
        private readonly string _bucketName;
        private readonly string _region;
        private readonly bool _usePublicReadAcl;

        public AWSS3Service(
            IAmazonS3 s3Client,
            IConfiguration configuration,
            ILogger<AWSS3Service> logger)
        {
            _s3Client = s3Client;
            _logger = logger;
            _bucketName = configuration["AWSS3:BucketName"] ?? "file-management";
            _region = configuration["AWSS3:Region"] ?? "us-east-1";
            _usePublicReadAcl = bool.TryParse(configuration["AWSS3:UsePublicReadAcl"], out var v) && v;
        }

        public async Task<(string Url, string Key)> UploadFileAsync(UploadFileRequest file, string? bucketName = null, string? keyPrefix = null)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            // Validate file size (500MB max)
            const long maxSize = 500 * 1024 * 1024; // 500MB
            if (file.Length > maxSize)
                throw new ArgumentException("File size exceeds 500MB limit", nameof(file));

            bucketName ??= _bucketName;

            var fileExtension = Path.GetExtension(file.FileName);
            var baseKey = $"{Guid.NewGuid():N}{fileExtension}";
            var s3Key = string.IsNullOrWhiteSpace(keyPrefix)
                ? baseKey
                : $"{keyPrefix.TrimEnd('/')}/{baseKey}";

            var uploadRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                InputStream = file.Content,
                ContentType = file.ContentType ?? "application/octet-stream"
            };

            // Buckets with Object Ownership = "Bucket owner enforced" do not allow ACLs.
            // In that case, omit the ACL and use a bucket policy or presigned URLs for access.
            if (_usePublicReadAcl)
                uploadRequest.CannedACL = S3CannedACL.PublicRead;

            uploadRequest.Metadata.Add("original-filename", file.FileName);
            uploadRequest.Metadata.Add("upload-date", DateTime.UtcNow.ToString("o"));

            var response = await _s3Client.PutObjectAsync(uploadRequest);
            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogError($"S3 upload failed with status: {response.HttpStatusCode}");
                throw new InvalidOperationException($"Upload failed: {response.HttpStatusCode}");
            }

            var s3Url = $"https://{bucketName}.s3.{_region}.amazonaws.com/{s3Key}";
            _logger.LogInformation($"File uploaded successfully to S3. Key: {s3Key}, URL: {s3Url}");

            return (s3Url, s3Key);
        }

        public async Task<bool> DeleteFileAsync(string fileKey, string? bucketName = null)
        {
            bucketName ??= _bucketName;

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = fileKey
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);
            if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent ||
                response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation($"File deleted successfully from S3. Key: {fileKey}");
                return true;
            }

            _logger.LogWarning($"Failed to delete file. Key: {fileKey}, Status: {response.HttpStatusCode}");
            return false;
        }

        public async Task<bool> DeleteRangeAsync(string[] fileKeys, string? bucketName = null)
        {
            bucketName ??= _bucketName;

            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = new List<KeyVersion>()
            };

            foreach (var key in fileKeys)
                deleteRequest.Objects.Add(new KeyVersion { Key = key });

            await _s3Client.DeleteObjectsAsync(deleteRequest);
            _logger.LogInformation($"Multiple files deleted from S3. Count: {fileKeys.Length}");
            return true;
        }

        public Task<string> CreateUploadUrlAsync(string fileKey, string contentType, string? bucketName = null, int expirationMinutes = 15)
        {
            bucketName ??= _bucketName;

            if (expirationMinutes < 1) expirationMinutes = 1;
            if (expirationMinutes > 60) expirationMinutes = 60;

            var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;

            _logger.LogInformation(
                "Creating S3 presigned PUT url. Bucket={Bucket} Key={Key} Region={Region} ContentType={ContentType} ExpiresMinutes={ExpiresMinutes}",
                bucketName,
                fileKey,
                _region,
                normalizedContentType,
                expirationMinutes);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = fileKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                ContentType = normalizedContentType,
                // Ensure the browser-sent Content-Type is part of the signature to avoid
                // mismatches/redirects that can surface as opaque 400s from S3.
                Protocol = Protocol.HTTPS
            };

            var url = _s3Client.GetPreSignedURL(request);
            return Task.FromResult(url);
        }

        public async Task<string> CreateFolderAsync(string folderKey, string? bucketName = null)
        {
            if (string.IsNullOrWhiteSpace(folderKey))
                throw new ArgumentException("Folder key is required", nameof(folderKey));

            bucketName ??= _bucketName;
            var normalizedKey = folderKey.TrimEnd('/') + "/";

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = normalizedKey,
                ContentBody = string.Empty,
                ContentType = "application/x-directory"
            };

            if (_usePublicReadAcl)
                request.CannedACL = S3CannedACL.PublicRead;

            await _s3Client.PutObjectAsync(request);

            var s3Url = $"https://{bucketName}.s3.{_region}.amazonaws.com/{normalizedKey}";
            _logger.LogInformation("Created S3 folder placeholder. Key={Key} Url={Url}", normalizedKey, s3Url);
            return s3Url;
        }

        public async Task<string> GetFileUrlAsync(
            string fileKey,
            string? bucketName = null,
            int expirationMinutes = 60,
            string? responseContentDisposition = null,
            string? responseContentType = null)
        {
            bucketName ??= _bucketName;

            var metadataRequest = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = fileKey
            };

            try
            {
                await _s3Client.GetObjectMetadataAsync(metadataRequest);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException($"File not found in S3: {fileKey}", ex);
            }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = fileKey,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            if (!string.IsNullOrWhiteSpace(responseContentDisposition) || !string.IsNullOrWhiteSpace(responseContentType))
            {
                request.ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentDisposition = responseContentDisposition,
                    ContentType = responseContentType
                };
            }

            return _s3Client.GetPreSignedURL(request);
        }
    }
}
