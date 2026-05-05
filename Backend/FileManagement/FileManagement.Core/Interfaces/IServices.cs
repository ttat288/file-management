namespace FileManagement.Core.Interfaces
{
    public interface IFileService
    {
        Task<Core.Models.ApiResponse<Core.Models.FileDto>> UploadFileAsync(Guid ownerId, Core.Models.UploadFileRequest file, Guid? folderId = null);
        Task<Core.Models.ApiResponse<Core.Models.FileDto>> CreateFileAsync(Guid ownerId, string name, long size, string contentType, string blobUrl, string blobName, Guid? folderId = null);
        Task<Core.Models.ApiResponse<Core.Models.PagedResult<Core.Models.FileDto>>> GetFilesAsync(Guid ownerId, Guid? folderId = null, int pageNumber = 1, int pageSize = 20);
        Task<List<Core.Models.FileDto>> GetFilesForFolderAsync(Guid ownerId, Guid? folderId = null);
        Task<Core.Models.ApiResponse<Core.Models.FileDto>> GetFileAsync(Guid ownerId, Guid fileId);
        Task<Core.Models.ApiResponse<string>> GetFileUrlAsync(Guid ownerId, Guid fileId, int expirationMinutes = 60, bool download = false);
        Task<Core.Models.ApiResponse<Core.Models.FileDto>> RenameFileAsync(Guid ownerId, Guid fileId, string newName);
        Task<Core.Models.ApiResponse> DeleteFileAsync(Guid ownerId, Guid fileId);
        Task<Core.Models.ApiResponse<Core.Models.PagedResult<Core.Models.FileDto>>> SearchFilesAsync(Guid ownerId, string searchTerm, Guid? folderId = null, int pageNumber = 1, int pageSize = 20);
    }

    public interface IFolderService
    {
        Task<Core.Models.ApiResponse<Core.Models.FolderDto>> CreateFolderAsync(Guid ownerId, string name, Guid? parentId = null);
        Task<Core.Models.ApiResponse<Core.Models.PagedResult<Core.Models.FolderDto>>> GetFoldersAsync(Guid ownerId, Guid? parentId = null, int pageNumber = 1, int pageSize = 20);
        Task<Core.Models.ApiResponse<Core.Models.FolderDto>> GetFolderAsync(Guid ownerId, Guid folderId);
        Task<Core.Models.ApiResponse<Core.Models.FolderDto>> RenameFolderAsync(Guid ownerId, Guid folderId, string newName);
        Task<Core.Models.ApiResponse> DeleteFolderAsync(Guid ownerId, Guid folderId);
    }

    public interface IAWSS3Service
    {
        Task<(string Url, string Key)> UploadFileAsync(Core.Models.UploadFileRequest file, string? bucketName = null, string? keyPrefix = null);
        Task<bool> DeleteFileAsync(string fileKey, string? bucketName = null);
        Task<bool> DeleteRangeAsync(string[] fileKeys, string? bucketName = null);
        Task<string> CreateUploadUrlAsync(string fileKey, string contentType, string? bucketName = null, int expirationMinutes = 15);
        Task<string> CreateFolderAsync(string folderKey, string? bucketName = null);
        Task<string> GetFileUrlAsync(
            string fileKey,
            string? bucketName = null,
            int expirationMinutes = 60,
            string? responseContentDisposition = null,
            string? responseContentType = null);
    }

    public interface IAzureBlobStorageService
    {
        Task<(string Url, string Key)> UploadFileAsync(Core.Models.UploadFileRequest file, string? containerName = null);
        Task<bool> DeleteFileAsync(string fileKey, string containerName = null);
        Task<bool> DeleteRangeAsync(string[] fileKeys, string containerName = null);
        Task<string> GetFileUrlAsync(string fileKey, string containerName = null, int expirationMinutes = 60);
    }

    public interface ICloudinaryService
    {
        Task<(string Url, string PublicId)> UploadFileAsync(Core.Models.UploadFileRequest file);
        Task<bool> DeleteFileAsync(string publicId);
        Task<string> GetFileUrlAsync(string publicId, int expirationMinutes = 60);
    }
}
