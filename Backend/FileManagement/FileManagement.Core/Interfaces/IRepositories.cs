using FileManagement.Core.Models;

namespace FileManagement.Core.Interfaces
{
    public interface IFileRepository
    {
        Task<FileDto?> CreateAsync(Guid ownerId, string name, long size, string contentType, string blobUrl, string blobName, Guid? folderId);
        Task<PagedResult<FileDto>> GetListAsync(Guid ownerId, Guid? folderId = null, int pageNumber = 1, int pageSize = 20);
        Task<FileDto?> GetByIdAsync(Guid ownerId, Guid fileId);
        Task<FileDto?> RenameAsync(Guid ownerId, Guid fileId, string newName);
        Task<(bool Success, string? BlobName)> DeleteAsync(Guid ownerId, Guid fileId);
        Task<PagedResult<FileDto>> SearchAsync(Guid ownerId, string searchTerm, Guid? folderId = null, int pageNumber = 1, int pageSize = 20);
    }

    public interface IFolderRepository
    {
        Task<FolderDto?> CreateAsync(Guid ownerId, string name, Guid? parentId);
        Task<PagedResult<FolderDto>> GetListAsync(Guid ownerId, Guid? parentId = null, int pageNumber = 1, int pageSize = 20);
        Task<FolderDto?> GetByIdAsync(Guid ownerId, Guid folderId);
        Task<FolderDto?> RenameAsync(Guid ownerId, Guid folderId, string newName);
        Task<bool> DeleteAsync(Guid ownerId, Guid folderId);
    }
}
