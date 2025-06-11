namespace AiMediaSync.Infrastructure.Storage;

public interface IFileStorage
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> ListFilesAsync(string prefix = "", CancellationToken cancellationToken = default);
    Task<string> GetFileUrlAsync(string filePath, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<long> GetFileSizeAsync(string filePath, CancellationToken cancellationToken = default);
}