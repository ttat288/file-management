using Dapper;
using Npgsql;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using FileManagement.Data.Utils;

namespace FileManagement.Data.Repositories
{
    public class FolderRepository : IFolderRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<FolderRepository> _logger;

        public FolderRepository(IConfiguration configuration, ILogger<FolderRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgreSQL") 
                ?? throw new InvalidOperationException("PostgreSQL connection string not found");
            _logger = logger;
        }

        private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<FolderDto?> CreateAsync(Guid ownerId, string name, Guid? parentId = null)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var result = await connection.QueryAsync<FolderDto>(
                    "SELECT * FROM public.fn_folder_create(@p_owner_id, @p_name, @p_parent_id)",
                    new
                    {
                        p_owner_id = ownerId,
                        p_name = name,
                        p_parent_id = parentId
                    }
                );

                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating folder: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_folder_create(uuid, varchar, uuid)", "Database/02_create_functions.sql");
            }
        }

        public async Task<PagedResult<FolderDto>> GetListAsync(Guid ownerId, Guid? parentId = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var results = await connection.QueryAsync<dynamic>(
                    "SELECT * FROM public.fn_folder_get_list(@p_owner_id, @p_parent_id, @p_page_number, @p_page_size)",
                    new
                    {
                        p_owner_id = ownerId,
                        p_parent_id = parentId,
                        p_page_number = pageNumber,
                        p_page_size = pageSize
                    }
                );

                var resultList = results.ToList();
                if (!resultList.Any())
                    return new PagedResult<FolderDto> { PageNumber = pageNumber, PageSize = pageSize };

                var folders = resultList.Select(r => new FolderDto
                {
                    Id = (Guid)r.id,
                    Name = (string)r.name,
                    ParentId = (Guid?)r.parent_id,
                    CreatedAt = (DateTime)r.created_at
                }).ToList();

                long totalCount = (long)resultList.First().total_count;

                return new PagedResult<FolderDto>
                {
                    Items = folders,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting folder list: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_folder_get_list(uuid, uuid, integer, integer)", "Database/02_create_functions.sql");
            }
        }

        public async Task<FolderDto?> GetByIdAsync(Guid ownerId, Guid folderId)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var result = await connection.QueryAsync<FolderDto>(
                    "SELECT * FROM public.fn_folder_get_by_id(@p_owner_id, @p_folder_id)",
                    new { p_owner_id = ownerId, p_folder_id = folderId }
                );

                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting folder: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_folder_get_by_id(uuid, uuid)", "Database/02_create_functions.sql");
            }
        }

        public async Task<FolderDto?> RenameAsync(Guid ownerId, Guid folderId, string newName)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var result = await connection.QueryAsync<FolderDto>(
                    "SELECT * FROM public.fn_folder_rename(@p_owner_id, @p_folder_id, @p_new_name)",
                    new { p_owner_id = ownerId, p_folder_id = folderId, p_new_name = newName }
                );

                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renaming folder: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_folder_rename(uuid, uuid, varchar)", "Database/02_create_functions.sql");
            }
        }

        public async Task<bool> DeleteAsync(Guid ownerId, Guid folderId)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var deletedId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                    "DELETE FROM folders WHERE owner_id = @ownerId AND id = @folderId RETURNING id",
                    new { ownerId, folderId }
                );

                return deletedId.HasValue;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting folder: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> HasContentsAsync(Guid ownerId, Guid folderId)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                // Check for files in the folder
                var fileCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM files WHERE owner_id = @ownerId AND folder_id = @folderId",
                    new { ownerId, folderId }
                );

                if (fileCount > 0) return true;

                // Check for subfolders
                var subfolderCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM folders WHERE owner_id = @ownerId AND parent_id = @folderId",
                    new { ownerId, folderId }
                );

                return subfolderCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking folder contents: {ex.Message}");
                throw;
            }
        }
    }
}
