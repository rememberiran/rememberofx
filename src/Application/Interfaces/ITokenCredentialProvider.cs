using Azure.Core;

namespace Application.Interfaces;

public interface ITokenCredentialProvider
{
    TokenCredential Credential { get; }
}
