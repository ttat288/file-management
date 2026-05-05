using Microsoft.AspNetCore.Mvc;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using FileManagement.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using FileManagement.Api.Realtime;

namespace FileManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IAWSS3Service _s3;
        private readonly EventBus _events;
        private readonly ILogger<FilesController> _logger;

        public FilesController(IFileService fileService, IAWSS3Service s3, EventBus events, ILogger<FilesController> logger)
        {
            _fileService = fileService;
            _s3 = s3;
            _events = events;
            _logger = logger;
        }

        /// <summary>
        /// Upload a new file
        /// </summary>
        /// <param name="file">File to upload</param>
        /// <param name="folderId">Optional folder ID</param>
        /// <returns>Uploaded file details</returns>
        [HttpPost("upload")]
        public async Task<ActionResult<ApiResponse<FileDto>>> UploadFile(IFormFile file, [FromQuery] Guid? folderId = null)
        {
            var ownerId = UserContext.GetUserId(User);
            _logger.LogInformation($"Upload request received. File: {file?.FileName}, Size: {file?.Length}");

            if (file == null)
                return BadRequest(ApiResponse<FileDto>.Error("File is required"));

            await using var stream = file.OpenReadStream();
            var upload = new UploadFileRequest(stream, file.FileName, file.ContentType, file.Length);
            var response = await _fileService.UploadFileAsync(ownerId, upload, folderId);

            if (!response.Success)
                return BadRequest(response);

            _events.Publish(new { type = "file_created", at = DateTime.UtcNow.ToString("o") });
            return Ok(response);
        }

        public record CreateUploadUrlRequest(string FileName, string ContentType, Guid? FolderId);
        public record CreateUploadUrlResponse(string UploadUrl, string Key);

        [HttpPost("upload-url")]
        public async Task<ActionResult<ApiResponse<CreateUploadUrlResponse>>> CreateUploadUrl([FromBody] CreateUploadUrlRequest req)
        {
            var ownerId = UserContext.GetUserId(User);

            if (string.IsNullOrWhiteSpace(req.FileName))
                return BadRequest(ApiResponse<CreateUploadUrlResponse>.Error("FileName is required"));

            var ext = Path.GetExtension(req.FileName);
            var folderSegment = req.FolderId.HasValue ? $"{req.FolderId.Value:N}/" : string.Empty;
            var key = $"{ownerId:N}/{folderSegment}{Guid.NewGuid():N}{ext}";

            try
            {
                _logger.LogInformation("CreateUploadUrl request. FileName={FileName} ContentType={ContentType} Key={Key} FolderId={FolderId}",
                    req.FileName,
                    req.ContentType,
                    key,
                    req.FolderId);

                var url = await _s3.CreateUploadUrlAsync(key, req.ContentType ?? "application/octet-stream", expirationMinutes: 15);
                _logger.LogInformation("Presigned URL created successfully. Bucket URL pattern: https://file-management-project.s3.amazonaws.com/... ContentType used: {ContentType}", req.ContentType ?? "application/octet-stream");
                return Ok(ApiResponse<CreateUploadUrlResponse>.Ok(new CreateUploadUrlResponse(url, key)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create upload url. FileName={FileName} ContentType={ContentType} Error={ErrorMessage} StackTrace={StackTrace}",
                    req.FileName,
                    req.ContentType,
                    ex.Message,
                    ex.StackTrace);
                return StatusCode(500, ApiResponse<CreateUploadUrlResponse>.Error($"Failed to create upload url: {ex.Message}"));
            }
        }

        public record CheckFileNameRequest(string FileName, Guid? FolderId);
        public record CheckFileNameResponse(bool Exists, string SuggestedName);

        /// <summary>
        /// Check if file name exists (Google Drive style)
        /// If exists, suggest a new name like "file (1).png"
        /// </summary>
        [HttpPost("check-filename")]
        public async Task<ActionResult<ApiResponse<CheckFileNameResponse>>> CheckFileName([FromBody] CheckFileNameRequest req)
        {
            var ownerId = UserContext.GetUserId(User);

            if (string.IsNullOrWhiteSpace(req.FileName))
                return BadRequest(ApiResponse<CheckFileNameResponse>.Error("FileName is required"));

            try
            {
                // Get all files in the folder to check for name conflicts
                var allFiles = await _fileService.GetFilesForFolderAsync(ownerId, req.FolderId);

                // Extract base name (without extension) from request
                var requestBaseNameWithoutExt = Path.GetFileNameWithoutExtension(req.FileName);

                // Check if file with same base name exists
                var existingFile = allFiles.FirstOrDefault(f =>
                {
                    var existingBaseName = Path.GetFileNameWithoutExtension(f.Name);
                    return existingBaseName.Equals(requestBaseNameWithoutExt, StringComparison.OrdinalIgnoreCase);
                });

                if (existingFile == null)
                    return Ok(ApiResponse<CheckFileNameResponse>.Ok(new CheckFileNameResponse(false, requestBaseNameWithoutExt)));

                var suggestedName = requestBaseNameWithoutExt;
                var counter = 1;

                // Keep incrementing until we find a name that doesn't exist by base name
                while (allFiles.Any(f =>
                {
                    var baseNameToCheck = Path.GetFileNameWithoutExtension(f.Name);
                    return baseNameToCheck.Equals(suggestedName, StringComparison.OrdinalIgnoreCase);
                }))
                {
                    suggestedName = $"{requestBaseNameWithoutExt} ({counter})";
                    counter++;
                }

                _logger.LogInformation("File name conflict detected (base name). Original={Original} Suggested={Suggested}", req.FileName, suggestedName);
                return Ok(ApiResponse<CheckFileNameResponse>.Ok(new CheckFileNameResponse(true, suggestedName)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file name: {Message}", ex.Message);
                return StatusCode(500, ApiResponse<CheckFileNameResponse>.Error($"Error checking file name: {ex.Message}"));
            }
        }

        public record CreateFileMetadataRequest(string Name, string Key, long Size, string ContentType, Guid? FolderId);

        [HttpPost]
        public async Task<ActionResult<ApiResponse<FileDto>>> CreateFile([FromBody] CreateFileMetadataRequest req)
        {
            var ownerId = UserContext.GetUserId(User);
            if (string.IsNullOrWhiteSpace(req.Key))
                return BadRequest(ApiResponse<FileDto>.Error("Key is required"));

            var requiredPrefix = $"{ownerId:N}/";
            if (!req.Key.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<FileDto>.Error("Invalid key for current user"));

            var blobUrl = ""; // keep empty; actual access via presigned GET
            var response = await _fileService.CreateFileAsync(ownerId, req.Name, req.Size, req.ContentType ?? "application/octet-stream", blobUrl, req.Key, req.FolderId);
            if (!response.Success) return BadRequest(response);
            _events.Publish(new { type = "file_created", at = DateTime.UtcNow.ToString("o") });
            return Ok(response);
        }

        /// <summary>
        /// Get paginated list of files
        /// </summary>
        /// <param name="folderId">Optional folder filter</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>Paginated file list</returns>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<FileDto>>>> GetFiles(
            [FromQuery] Guid? folderId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var ownerId = UserContext.GetUserId(User);
            _logger.LogInformation($"Get files request. FolderId: {folderId}, Page: {pageNumber}, Size: {pageSize}");

            var response = await _fileService.GetFilesAsync(ownerId, folderId, pageNumber, pageSize);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Get a specific file by ID
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>File details</returns>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<FileDto>>> GetFile(Guid id)
        {
            var ownerId = UserContext.GetUserId(User);
            _logger.LogInformation($"Get file request. FileId: {id}");

            var response = await _fileService.GetFileAsync(ownerId, id);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        /// <summary>
        /// Get a presigned download/view URL for a file (S3)
        /// </summary>
        /// <param name="id">File ID</param>
        /// <param name="expires">Expiration in minutes (default: 60)</param>
        [HttpGet("{id:guid}/url")]
        public async Task<ActionResult<ApiResponse<string>>> GetFileUrl(Guid id, [FromQuery] int expires = 60, [FromQuery] bool download = false)
        {
            var ownerId = UserContext.GetUserId(User);
            _logger.LogInformation($"Get file url request. FileId: {id}, Expires: {expires}, Download: {download}");

            var response = await _fileService.GetFileUrlAsync(ownerId, id, expires, download);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Rename a file
        /// </summary>
        /// <param name="id">File ID</param>
        /// <param name="request">New file name</param>
        /// <returns>Updated file details</returns>
        [HttpPut("{id:guid}/rename")]
        public async Task<ActionResult<ApiResponse<FileDto>>> RenameFile(Guid id, [FromBody] RenameFileRequest request)
        {
            var ownerId = UserContext.GetUserId(User);
            _logger.LogInformation($"Rename file request. FileId: {id}, NewName: {request?.NewName}");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _fileService.RenameFileAsync(ownerId, id, request.NewName);

            if (!response.Success)
                return BadRequest(response);

            _events.Publish(new { type = "file_renamed", at = DateTime.UtcNow.ToString("o") });
            return Ok(response);
        }

        /// <summary>
        /// Delete a file
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResponse>> DeleteFile(Guid id)
        {
            var ownerId = UserContext.GetUserId(User);
            _logger.LogInformation($"Delete file request. FileId: {id}");

            var response = await _fileService.DeleteFileAsync(ownerId, id);

            if (!response.Success)
                return BadRequest(response);

            _events.Publish(new { type = "file_deleted", at = DateTime.UtcNow.ToString("o") });
            return Ok(response);
        }

        /// <summary>
        /// Search files by name
        /// </summary>
        /// <param name="searchTerm">Search term</param>
        /// <param name="folderId">Optional folder filter</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>Search results</returns>
        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse<PagedResult<FileDto>>>> SearchFiles(
            [FromQuery] string searchTerm,
            [FromQuery] Guid? folderId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var ownerId = UserContext.GetUserId(User);
            _logger.LogInformation($"Search files request. Term: {searchTerm}, Page: {pageNumber}");

            var response = await _fileService.SearchFilesAsync(ownerId, searchTerm, folderId, pageNumber, pageSize);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }
    }
}
