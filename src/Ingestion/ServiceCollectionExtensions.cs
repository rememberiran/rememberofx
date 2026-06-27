using Ingestion.Twitter;
using Microsoft.Extensions.DependencyInjection;

namespace Ingestion;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIngestion(this IServiceCollection services)
    {
        services.AddSingleton<ITweetScraper, TweetScraper>();

        services.AddHttpClient("TweetMediaDownloader", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
