namespace Ingestion.Twitter;

public interface ITweetScraper : IAsyncDisposable
{
    Task<ScrapedTweet> ScrapeAsync(string tweetUrl, CancellationToken ct = default);
}
