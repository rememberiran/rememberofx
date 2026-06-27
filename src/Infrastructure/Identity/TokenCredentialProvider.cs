using Application.Interfaces;
using Azure.Core;
using Azure.Identity;

namespace Infrastructure.Identity;

public class TokenCredentialProvider : ITokenCredentialProvider
{
    private static readonly TokenCredential Instance = new DefaultAzureCredential();

    public TokenCredential Credential => Instance;
}
