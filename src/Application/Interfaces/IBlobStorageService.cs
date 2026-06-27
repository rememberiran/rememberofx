namespace Application.Interfaces;

public interface IBlobStorageService
{
    string? GetScreenshotSasUrl(string? blobName);
    string? GetMediaSasUrl(string? blobName);
    Task<string> UploadScreenshotAsync(string blobName, ReadOnlyMemory<byte> data, string contentType, CancellationToken ct);
    Task<string> UploadMediaAsync(string blobName, ReadOnlyMemory<byte> data, string contentType, CancellationToken ct);
}
