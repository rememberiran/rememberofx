using Application.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.BlobStorage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _screenshotsContainer;
    private readonly BlobContainerClient _mediaContainer;

    public BlobStorageService(IConfiguration configuration, ITokenCredentialProvider credentialProvider)
    {
        var accountUrl = configuration[$"BlobStorage:AccountUrl"]!;
        var serviceClient = new BlobServiceClient(new Uri(accountUrl), credentialProvider.Credential);
        _screenshotsContainer = serviceClient.GetBlobContainerClient($"screenshots");
        _mediaContainer = serviceClient.GetBlobContainerClient($"media");
    }

    public string? GetScreenshotSasUrl(string? blobName)
    {
        return GenerateSasUrl(_screenshotsContainer, blobName);
    }

    public string? GetMediaSasUrl(string? blobName)
    {
        return GenerateSasUrl(_mediaContainer, blobName);
    }

    public async Task<string> UploadScreenshotAsync(string blobName, ReadOnlyMemory<byte> data, string contentType, CancellationToken ct)
    {
        return await UploadAsync(_screenshotsContainer, blobName, data, contentType, ct);
    }

    public async Task<string> UploadMediaAsync(string blobName, ReadOnlyMemory<byte> data, string contentType, CancellationToken ct)
    {
        return await UploadAsync(_mediaContainer, blobName, data, contentType, ct);
    }

    private static async Task<string> UploadAsync(BlobContainerClient container, string blobName, ReadOnlyMemory<byte> data, string contentType, CancellationToken ct)
    {
        var blobClient = container.GetBlobClient(blobName);
        await blobClient.UploadAsync(
            new BinaryData(data),
            new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType },
            },
            ct);
        return blobName;
    }

    private static string? GenerateSasUrl(BlobContainerClient container, string? blobName)
    {
        if (string.IsNullOrEmpty(blobName))
        {
            return null;
        }

        var blobClient = container.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            return blobClient.Uri.ToString();
        }

        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
        return sasUri.ToString();
    }
}
