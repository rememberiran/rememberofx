namespace Application.Interfaces;

public interface IBlobStorageService
{
    string? GetScreenshotSasUrl(string? blobName);
}
