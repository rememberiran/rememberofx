using Application.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.BlobStorage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _screenshotsContainer;
    private readonly BlobContainerClient _mediaContainer;

    public BlobStorageService(IConfiguration configuration)
    {
        var accountUrl = configuration[$"BlobStorage:AccountUrl"]!;
        var serviceClient = new BlobServiceClient(new Uri(accountUrl), new Azure.Identity.DefaultAzureCredential());
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
