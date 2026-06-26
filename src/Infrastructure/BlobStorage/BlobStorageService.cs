using Application.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.BlobStorage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageService(IConfiguration configuration)
    {
        var accountUrl = configuration["BlobStorage:AccountUrl"]!;
        var serviceClient = new BlobServiceClient(new Uri(accountUrl), new Azure.Identity.DefaultAzureCredential());
        _containerClient = serviceClient.GetBlobContainerClient("screenshots");
    }

    public string? GetScreenshotSasUrl(string? blobName)
    {
        if (string.IsNullOrEmpty(blobName))
            return null;

        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
            return blobClient.Uri.ToString();

        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
        return sasUri.ToString();
    }
}
