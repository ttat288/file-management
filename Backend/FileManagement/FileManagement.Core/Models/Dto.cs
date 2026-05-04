namespace FileManagement.Core.Models
{
    public class FileDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public string BlobUrl { get; set; }
        public string BlobName { get; set; }
        public Guid? FolderId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FolderDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid? ParentId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateFileRequest
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public Guid? FolderId { get; set; }
    }

    public class RenameFileRequest
    {
        public string NewName { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => PageNumber < TotalPages;
        public bool HasPreviousPage => PageNumber > 1;
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }

        public static ApiResponse<T> Ok(T data, string message = null)
            => new() { Success = true, Data = data, Message = message };

        public static ApiResponse<T> Error(string message)
            => new() { Success = false, Message = message };
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static ApiResponse Ok(string message = "Success")
            => new() { Success = true, Message = message };

        public static ApiResponse Error(string message)
            => new() { Success = false, Message = message };
    }
}