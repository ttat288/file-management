using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace FileManagement.Core.Services
{
    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;
        private readonly IAWSS3Service _s3Service;
        private readonly ILogger<FileService> _logger;

        // Validation constants
        private const long MaxFileSize = 100 * 1024 * 1024; // 100MB
        private static readonly string[] AllowedMimeTypes = { "image/", "application/pdf", "text/", "video/" };

        public FileService(
            IFileRepository fileRepository,
            IAWSS3Service s3Service,
            ILogger<FileService> logger)
        {
            _fileRepository = fileRepository;
            _s3Service = s3Service;
            _logger = logger;
        }

        public async Task<ApiResponse<FileDto>> UploadFileAsync(Guid ownerId, UploadFileRequest file, Guid? folderId = null)
        {
            try
            {
                // Validate file
                var validationError = ValidateFile(file);
                if (validationError != null)
                    return ApiResponse<FileDto>.Error(validationError);

                _logger.LogInformation($"Starting file upload: {file.FileName}");

                // Upload to S3 with a folder-aware prefix so uploaded files are grouped by user and folder.
                var keyPrefix = folderId.HasValue ? $"{ownerId:N}/{folderId.Value:N}" : $"{ownerId:N}";
                var (s3Url, s3Key) = await _s3Service.UploadFileAsync(file, keyPrefix: keyPrefix);

                // Save file metadata to database
                var fileDto = await _fileRepository.CreateAsync(
                    ownerId: ownerId,
                    name: Path.GetFileNameWithoutExtension(file.FileName),
                    size: file.Length,
                    contentType: file.ContentType ?? "application/octet-stream",
                    blobUrl: s3Url,
                    blobName: s3Key,
                    folderId: folderId
                );

                if (fileDto == null)
                {
                    // Rollback: delete from S3 if DB insert fails
                    await _s3Service.DeleteFileAsync(s3Key);
                    return ApiResponse<FileDto>.Error("Failed to save file metadata");
                }

                _logger.LogInformation($"File uploaded successfully: {fileDto.Id}");
                return ApiResponse<FileDto>.Ok(fileDto, "File uploaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return ApiResponse<FileDto>.Error($"Upload failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse<FileDto>> CreateFileAsync(Guid ownerId, string name, long size, string contentType, string blobUrl, string blobName, Guid? folderId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return ApiResponse<FileDto>.Error("File name is required");

                if (size <= 0)
                    return ApiResponse<FileDto>.Error("Invalid file size");

                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(name.Trim());
                var fileDto = await _fileRepository.CreateAsync(ownerId, fileNameWithoutExtension, size, contentType, blobUrl, blobName, folderId);
                if (fileDto == null)
                    return ApiResponse<FileDto>.Error("Failed to save file metadata");

                return ApiResponse<FileDto>.Ok(fileDto, "File created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating file metadata: {ex.Message}");
                return ApiResponse<FileDto>.Error($"Create failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PagedResult<FileDto>>> GetFilesAsync(Guid ownerId, Guid? folderId = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Validate pagination parameters
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100; // Max page size

                var result = await _fileRepository.GetListAsync(ownerId, folderId, pageNumber, pageSize);
                return ApiResponse<PagedResult<FileDto>>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting files: {ex.Message}");
                return ApiResponse<PagedResult<FileDto>>.Error($"Failed to fetch files: {ex.Message}");
            }
        }

        public async Task<List<FileDto>> GetFilesForFolderAsync(Guid ownerId, Guid? folderId = null)
        {
            try
            {
                var allFiles = new List<FileDto>();
                int pageNumber = 1;
                const int pageSize = 100;

                while (true)
                {
                    var result = await _fileRepository.GetListAsync(ownerId, folderId, pageNumber, pageSize);
                    if (result.Items == null || result.Items.Count == 0)
                        break;

                    allFiles.AddRange(result.Items);

                    // Stop if we've fetched all pages
                    if (result.Items.Count < pageSize)
                        break;

                    pageNumber++;
                }

                return allFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting files for folder: {ex.Message}");
                return new List<FileDto>();
            }
        }

        public async Task<ApiResponse<FileDto>> GetFileAsync(Guid ownerId, Guid fileId)
        {
            try
            {
                var file = await _fileRepository.GetByIdAsync(ownerId, fileId);
                if (file == null)
                    return ApiResponse<FileDto>.Error("File not found");

                return ApiResponse<FileDto>.Ok(file);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting file: {ex.Message}");
                return ApiResponse<FileDto>.Error($"Failed to fetch file: {ex.Message}");
            }
        }

        public async Task<ApiResponse<string>> GetFileUrlAsync(Guid ownerId, Guid fileId, int expirationMinutes = 60, bool download = false)
        {
            try
            {
                if (expirationMinutes < 1) expirationMinutes = 1;
                if (expirationMinutes > 24 * 60) expirationMinutes = 24 * 60;

                var file = await _fileRepository.GetByIdAsync(ownerId, fileId);
                if (file == null)
                    return ApiResponse<string>.Error("File not found");

                if (string.IsNullOrWhiteSpace(file.BlobName))
                    return ApiResponse<string>.Error("File storage key not found");

                string? contentDisposition = null;
                string? contentType = null;

                if (download)
                {
                    contentType = file.ContentType;

                    var extension = Path.GetExtension(file.BlobName ?? "");
                    var fullFileName = string.IsNullOrWhiteSpace(extension) ? file.Name : $"{file.Name}{extension}";
                    var safeFileName = fullFileName.Replace("\"", "").Replace("\r", "").Replace("\n", "");
                    contentDisposition = $"attachment; filename=\"{safeFileName}\"";
                }

                var url = await _s3Service.GetFileUrlAsync(
                    file.BlobName,
                    expirationMinutes: expirationMinutes,
                    responseContentDisposition: contentDisposition,
                    responseContentType: contentType);
                return ApiResponse<string>.Ok(url);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating file URL: {ex.Message}");
                return ApiResponse<string>.Error($"Failed to create file URL: {ex.Message}");
            }
        }

        public async Task<ApiResponse<FileDto>> RenameFileAsync(Guid ownerId, Guid fileId, string newName)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(newName))
                    return ApiResponse<FileDto>.Error("New name cannot be empty");

                var normalizedNewName = Path.GetFileNameWithoutExtension(newName.Trim());
                if (normalizedNewName.Length > 255)
                    return ApiResponse<FileDto>.Error("File name cannot exceed 255 characters");

                var file = await _fileRepository.RenameAsync(ownerId, fileId, normalizedNewName);
                if (file == null)
                    return ApiResponse<FileDto>.Error("File not found or rename failed");

                _logger.LogInformation($"File renamed: {fileId}");
                return ApiResponse<FileDto>.Ok(file, "File renamed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renaming file: {ex.Message}");
                return ApiResponse<FileDto>.Error($"Rename failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse> DeleteFileAsync(Guid ownerId, Guid fileId)
        {
            try
            {
                var (success, blobName) = await _fileRepository.DeleteAsync(ownerId, fileId);
                if (!success)
                    return ApiResponse.Error("File not found");

                // Delete from S3
                if (!string.IsNullOrEmpty(blobName))
                {
                    try
                    {
                        await _s3Service.DeleteFileAsync(blobName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to delete file from S3: {ex.Message}");
                        // Continue - file is already removed from DB
                    }
                }

                _logger.LogInformation($"File deleted: {fileId}");
                return ApiResponse.Ok("File deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting file: {ex.Message}");
                return ApiResponse.Error($"Delete failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PagedResult<FileDto>>> SearchFilesAsync(Guid ownerId, string searchTerm, Guid? folderId = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return ApiResponse<PagedResult<FileDto>>.Error("Search term cannot be empty");

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var result = await _fileRepository.SearchAsync(ownerId, searchTerm.Trim(), folderId, pageNumber, pageSize);
                return ApiResponse<PagedResult<FileDto>>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching files: {ex.Message}");
                return ApiResponse<PagedResult<FileDto>>.Error($"Search failed: {ex.Message}");
            }
        }

        private string ValidateFile(UploadFileRequest file)
        {
            if (file == null || file.Length == 0)
                return "File is required and cannot be empty";

            if (file.Length > MaxFileSize)
                return "File size exceeds maximum limit of 100MB";

            if (string.IsNullOrWhiteSpace(file.FileName))
                return "File name is required";

            if (file.FileName.Length > 255)
                return "File name cannot exceed 255 characters";

            // Optional: Validate MIME type
            if (!IsAllowedMimeType(file.ContentType))
                return $"File type not allowed: {file.ContentType}";

            return null;
        }

        private bool IsAllowedMimeType(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
                return true; // Allow if not specified

            return AllowedMimeTypes.Any(allowed => mimeType.StartsWith(allowed));
        }
    }
}
