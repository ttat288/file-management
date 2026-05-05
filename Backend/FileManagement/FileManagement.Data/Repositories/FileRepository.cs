using Dapper;
using Npgsql;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using FileManagement.Data.Utils;

namespace FileManagement.Data.Repositories
{
    public class FileRepository : IFileRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<FileRepository> _logger;

        public FileRepository(IConfiguration configuration, ILogger<FileRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("PostgreSQL") 
                ?? throw new InvalidOperationException("PostgreSQL connection string not found");
            _logger = logger;
        }

        private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        private static FileDto MapFileRow(dynamic row)
        {
            return new FileDto
            {
                Id = (Guid)row.id,
                Name = (string)row.name,
                Size = (long)row.size,
                ContentType = (string)row.content_type,
                BlobUrl = (string)row.blob_url,
                BlobName = (string)row.blob_name,
                FolderId = (Guid?)row.folder_id,
                CreatedAt = (DateTime)row.created_at
            };
        }

        public async Task<FileDto?> CreateAsync(Guid ownerId, string name, long size, string contentType, string blobUrl, string blobName, Guid? folderId)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var result = await connection.QueryAsync<dynamic>(
                    @"INSERT INTO files (name, size, content_type, blob_url, blob_name, folder_id, owner_id)
                      VALUES (@p_name, @p_size, @p_content_type, @p_blob_url, @p_blob_name, @p_folder_id, @p_owner_id)
                      RETURNING id, name, size, content_type, blob_url, blob_name, folder_id, created_at",
                    new
                    {
                        p_owner_id = ownerId,
                        p_name = name,
                        p_size = size,
                        p_content_type = contentType,
                        p_blob_url = blobUrl,
                        p_blob_name = blobName,
                        p_folder_id = folderId
                    }
                );

                var row = result.FirstOrDefault();
                return row == null ? null : MapFileRow(row);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating file: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_file_create(uuid, varchar, bigint, varchar, text, varchar, uuid)", "Database/02_create_functions.sql");
            }
        }

        public async Task<PagedResult<FileDto>> GetListAsync(Guid ownerId, Guid? folderId = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var results = await connection.QueryAsync<dynamic>(
                    "SELECT * FROM public.fn_file_get_list(@p_owner_id, @p_folder_id, @p_page_number, @p_page_size)",
                    new
                    {
                        p_owner_id = ownerId,
                        p_folder_id = folderId,
                        p_page_number = pageNumber,
                        p_page_size = pageSize
                    }
                );

                var resultList = results.ToList();
                if (!resultList.Any())
                    return new PagedResult<FileDto> { PageNumber = pageNumber, PageSize = pageSize };

                var files = resultList.Select(r => new FileDto
                {
                    Id = (Guid)r.id,
                    Name = (string)r.name,
                    Size = (long)r.size,
                    ContentType = (string)r.content_type,
                    BlobUrl = (string)r.blob_url,
                    BlobName = (string)r.blob_name,
                    FolderId = (Guid?)r.folder_id,
                    CreatedAt = (DateTime)r.created_at
                }).ToList();

                long totalCount = (long)resultList.First().total_count;

                return new PagedResult<FileDto>
                {
                    Items = files,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting file list: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_file_get_list(uuid, uuid, integer, integer)", "Database/02_create_functions.sql");
            }
        }

        public async Task<FileDto?> GetByIdAsync(Guid ownerId, Guid fileId)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var result = await connection.QueryAsync<dynamic>(
                    "SELECT * FROM public.fn_file_get_by_id(@p_owner_id, @p_file_id)",
                    new { p_owner_id = ownerId, p_file_id = fileId }
                );

                var row = result.FirstOrDefault();
                return row == null ? null : MapFileRow(row);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting file: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_file_get_by_id(uuid, uuid)", "Database/02_create_functions.sql");
            }
        }

        public async Task<FileDto?> RenameAsync(Guid ownerId, Guid fileId, string newName)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var result = await connection.QueryAsync<dynamic>(
                    "SELECT * FROM public.fn_file_rename(@p_owner_id, @p_file_id, @p_new_name)",
                    new { p_owner_id = ownerId, p_file_id = fileId, p_new_name = newName }
                );

                var row = result.FirstOrDefault();
                return row == null ? null : MapFileRow(row);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error renaming file: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_file_rename(uuid, uuid, varchar)", "Database/02_create_functions.sql");
            }
        }

        public async Task<(bool Success, string? BlobName)> DeleteAsync(Guid ownerId, Guid fileId)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var result = await connection.QueryAsync<dynamic>(
                    "SELECT * FROM public.fn_file_delete(@p_owner_id, @p_file_id)",
                    new { p_owner_id = ownerId, p_file_id = fileId }
                );

                var row = result.FirstOrDefault();
                if (row != null)
                {
                    var blobName = (string?)row.blob_name;
                    var success = (bool)row.success;
                    return (success, blobName);
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting file: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_file_delete(uuid, uuid)", "Database/02_create_functions.sql");
            }
        }

        public async Task<PagedResult<FileDto>> SearchAsync(Guid ownerId, string searchTerm, Guid? folderId = null, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                using var connection = CreateConnection();
                await connection.OpenAsync();

                var results = await connection.QueryAsync<dynamic>(
                    "SELECT * FROM public.fn_file_search(@p_owner_id, @p_search_term, @p_folder_id, @p_page_number, @p_page_size)",
                    new
                    {
                        p_owner_id = ownerId,
                        p_search_term = searchTerm,
                        p_folder_id = folderId,
                        p_page_number = pageNumber,
                        p_page_size = pageSize
                    }
                );

                var resultList = results.ToList();
                if (!resultList.Any())
                    return new PagedResult<FileDto> { PageNumber = pageNumber, PageSize = pageSize };

                var files = resultList.Select(r => new FileDto
                {
                    Id = (Guid)r.id,
                    Name = (string)r.name,
                    Size = (long)r.size,
                    ContentType = (string)r.content_type,
                    BlobUrl = (string)r.blob_url,
                    BlobName = (string)r.blob_name,
                    FolderId = (Guid?)r.folder_id,
                    CreatedAt = (DateTime)r.created_at
                }).ToList();

                long totalCount = (long)resultList.First().total_count;

                return new PagedResult<FileDto>
                {
                    Items = files,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching files: {ex.Message}");
                throw PostgresErrorMapper.MapOrSame(ex, "public.fn_file_search(uuid, text, uuid, integer, integer)", "Database/02_create_functions.sql");
            }
        }
    }
}
