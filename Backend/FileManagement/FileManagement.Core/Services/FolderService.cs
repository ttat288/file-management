using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.Extensions.Logging;

namespace FileManagement.Core.Services
{
    public class FolderService : IFolderService
    {
        private readonly IFolderRepository _folders;
        private readonly ILogger<FolderService> _logger;

        public FolderService(IFolderRepository folders, ILogger<FolderService> logger)
        {
            _folders = folders;
            _logger = logger;
        }

        public async Task<ApiResponse<FolderDto>> CreateFolderAsync(Guid ownerId, string name, Guid? parentId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return ApiResponse<FolderDto>.Error("Folder name is required");

                var created = await _folders.CreateAsync(ownerId, name.Trim(), parentId);
                if (created == null)
                    return ApiResponse<FolderDto>.Error("Failed to create folder");

                return ApiResponse<FolderDto>.Ok(created, "Folder created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating folder: {ex.Message}");
                return ApiResponse<FolderDto>.Error($"Create failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PagedResult<FolderDto>>> GetFoldersAsync(Guid ownerId, Guid? parentId = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 500) pageSize = 500;

                var result = await _folders.GetListAsync(ownerId, parentId, pageNumber, pageSize);
                return ApiResponse<PagedResult<FolderDto>>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching folders: {ex.Message}");
                return ApiResponse<PagedResult<FolderDto>>.Error($"Failed to fetch folders: {ex.Message}");
            }
        }

        public async Task<ApiResponse<FolderDto>> GetFolderAsync(Guid ownerId, Guid folderId)
        {
            try
            {
                var folder = await _folders.GetByIdAsync(ownerId, folderId);
                if (folder == null)
                    return ApiResponse<FolderDto>.Error("Folder not found");

                return ApiResponse<FolderDto>.Ok(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching folder: {ex.Message}");
                return ApiResponse<FolderDto>.Error($"Failed to fetch folder: {ex.Message}");
            }
        }

        public async Task<ApiResponse<FolderDto>> RenameFolderAsync(Guid ownerId, Guid folderId, string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                    return ApiResponse<FolderDto>.Error("New name cannot be empty");

                var folder = await _folders.RenameAsync(ownerId, folderId, newName.Trim());
                if (folder == null)
                    return ApiResponse<FolderDto>.Error("Folder not found or rename failed");

                return ApiResponse<FolderDto>.Ok(folder, "Folder renamed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renaming folder: {ex.Message}");
                return ApiResponse<FolderDto>.Error($"Rename failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse> DeleteFolderAsync(Guid ownerId, Guid folderId)
        {
            try
            {
                var ok = await _folders.DeleteAsync(ownerId, folderId);
                if (!ok)
                    return ApiResponse.Error("Folder not found");

                return ApiResponse.Ok("Folder deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting folder: {ex.Message}");
                return ApiResponse.Error($"Delete failed: {ex.Message}");
            }
        }
    }
}

