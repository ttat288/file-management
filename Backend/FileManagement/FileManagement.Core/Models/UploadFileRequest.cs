using System.IO;

namespace FileManagement.Core.Models
{
    public sealed class UploadFileRequest
    {
        public UploadFileRequest(Stream content, string fileName, string? contentType, long length)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            FileName = string.IsNullOrWhiteSpace(fileName) ? throw new ArgumentException("FileName is required", nameof(fileName)) : fileName;
            ContentType = contentType;
            Length = length;
        }

        public Stream Content { get; }
        public string FileName { get; }
        public string? ContentType { get; }
        public long Length { get; }
    }
}
