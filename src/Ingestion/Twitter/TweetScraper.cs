using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Ingestion.Twitter;

public sealed partial class TweetScraper : ITweetScraper
{
    private const int MaxPoolSize = 5;
    private const int NavigationTimeoutMs = 20_000;
    private const int SelectorTimeoutMs = 30_000;
    private const int MediaWaitMs = 3_000;

    private static readonly EventId ScrapeCompletedEvent = new(5001, "TweetScrapeCompleted");
    private static readonly EventId ScrapeErrorEvent = new(5002, "TweetScrapeError");
    private static readonly EventId BrowserInitializedEvent = new(5003, "BrowserInitialized");
    private static readonly EventId ScreenshotFailedEvent = new(5004, "ScreenshotFailed");
    private static readonly EventId ImageDownloadErrorEvent = new(5005, "ImageDownloadError");
    private static readonly EventId VideoDownloadErrorEvent = new(5006, "VideoDownloadError");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TweetScraper> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ConcurrentBag<IBrowserContext> _contextPool = new();

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _isInitialized;

    public TweetScraper(IHttpClientFactory httpClientFactory, ILogger<TweetScraper> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ScrapedTweet> ScrapeAsync(string tweetUrl, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        var tweetId = ExtractTweetId(tweetUrl);
        var result = new ScrapedTweet { TweetUrl = tweetUrl };

        var context = await GetContextAsync();
        var page = await context.NewPageAsync();

        await SetupResourceBlockingAsync(page);

        var videoUrls = new List<string>();
        page.Response += (_, response) =>
        {
            var url = response.Url;
            if (url.Contains("video.twimg.com", StringComparison.OrdinalIgnoreCase) &&
                (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase) || url.Contains(".m4s", StringComparison.OrdinalIgnoreCase)))
            {
                videoUrls.Add(url);
            }
        };

        try
        {
            await page.GotoAsync(tweetUrl, new PageGotoOptions { Timeout = NavigationTimeoutMs });

            await page.WaitForSelectorAsync("article[data-testid='tweet']", new PageWaitForSelectorOptions { Timeout = SelectorTimeoutMs });

            await Task.WhenAny(
                page.WaitForSelectorAsync("article[data-testid='tweet'] img[src*='twimg.com/media']", new PageWaitForSelectorOptions { Timeout = MediaWaitMs }),
                Task.Delay(1500, ct));

            await TryPlayVideoAsync(page);

            if (await page.QuerySelectorAsync("article[data-testid='tweet'] div[data-testid='videoPlayer']") is not null)
            {
                await Task.Delay(2000, ct);
            }

            result.UserId = await ExtractUserIdAsync(page);
            result.UserName = await ExtractUserNameAsync(page);
            result.UserHandle = await ExtractUserHandleAsync(page);
            result.Text = await ExtractTweetTextAsync(page);
            result.Date = await ExtractDateAsync(page);
            result.Screenshot = await TakeScreenshotAsync(page, tweetId);
            result.Media = await DownloadMediaAsync(page, tweetId, videoUrls, ct);

            _logger.LogInformation(ScrapeCompletedEvent, "Tweet scraped: {TweetUrl}, media count: {MediaCount}", tweetUrl, result.Media.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ScrapeErrorEvent, ex, "Failed to scrape tweet {TweetUrl}", tweetUrl);
            throw;
        }
        finally
        {
            await page.CloseAsync();
            ReturnContext(context);
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        while (_contextPool.TryTake(out var context))
        {
            await context.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _initLock.Dispose();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                return;
            }

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args =
                [
                    "--no-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--disable-extensions",
                    "--disable-background-networking",
                    "--disable-default-apps",
                    "--disable-sync",
                    "--disable-translate",
                    "--metrics-recording-only",
                    "--mute-audio",
                    "--no-first-run",
                ],
            });
            _isInitialized = true;
            _logger.LogInformation(BrowserInitializedEvent, "Playwright browser initialized");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<IBrowserContext> GetContextAsync()
    {
        if (_contextPool.TryTake(out var context))
        {
            return context;
        }

        return await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            JavaScriptEnabled = true,
            BypassCSP = true,
        });
    }

    private void ReturnContext(IBrowserContext context)
    {
        if (_contextPool.Count < MaxPoolSize)
        {
            _contextPool.Add(context);
        }
        else
        {
            _ = context.CloseAsync();
        }
    }

    private static async Task SetupResourceBlockingAsync(IPage page)
    {
        await page.RouteAsync("**/*", async route =>
        {
            var request = route.Request;
            var resourceType = request.ResourceType;

            if (resourceType is "font" or "stylesheet" ||
                request.Url.Contains("analytics", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("ads", StringComparison.OrdinalIgnoreCase) ||
                request.Url.Contains("tracking", StringComparison.OrdinalIgnoreCase))
            {
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });
    }

    private static async Task TryPlayVideoAsync(IPage page)
    {
        try
        {
            var videoContainer = await page.QuerySelectorAsync("article[data-testid='tweet'] div[data-testid='videoPlayer']");
            if (videoContainer is not null)
            {
                await videoContainer.ClickAsync();
                return;
            }

            var playButton = await page.QuerySelectorAsync("article[data-testid='tweet'] div[role='button'][aria-label*='Play']");
            if (playButton is not null)
            {
                await playButton.ClickAsync();
            }
        }
        catch (PlaywrightException)
        {
        }
    }

    private static string ExtractTweetId(string tweetUrl)
    {
        var uri = new Uri(tweetUrl);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var statusIndex = Array.IndexOf(segments, "status");

        if (statusIndex >= 0 && statusIndex + 1 < segments.Length)
        {
            return segments[statusIndex + 1].Split('?')[0];
        }

        return DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ExtractUserIdAsync(IPage page)
    {
        try
        {
            var profileImg = await page.QuerySelectorAsync("article[data-testid='tweet'] img[src*='profile_images']");
            if (profileImg is not null)
            {
                var src = await profileImg.GetAttributeAsync("src");
                if (!string.IsNullOrEmpty(src))
                {
                    var match = ProfileImageIdRegex().Match(src);
                    if (match.Success)
                    {
                        return match.Groups["id"].Value;
                    }
                }
            }

            var scriptContent = await page.EvaluateAsync<string?>(@"() => {
                const scripts = document.querySelectorAll('script[type=""application/json""]');
                for (const script of scripts) {
                    if (script.textContent && script.textContent.includes('""user_id_str""')) {
                        return script.textContent;
                    }
                }
                return null;
            }");

            if (!string.IsNullOrEmpty(scriptContent))
            {
                var match = UserIdStrRegex().Match(scriptContent);
                if (match.Success)
                {
                    return match.Groups["id"].Value;
                }
            }
        }
        catch (PlaywrightException)
        {
        }

        return null;
    }

    private static async Task<string?> ExtractUserNameAsync(IPage page)
    {
        try
        {
            var el = await page.QuerySelectorAsync("article[data-testid='tweet'] div[data-testid='User-Name'] span");
            if (el is not null)
            {
                return await el.InnerTextAsync();
            }
        }
        catch (PlaywrightException)
        {
        }

        return null;
    }

    private static async Task<string?> ExtractUserHandleAsync(IPage page)
    {
        try
        {
            var elements = await page.QuerySelectorAllAsync("article[data-testid='tweet'] div[data-testid='User-Name'] a");
            foreach (var element in elements)
            {
                var href = await element.GetAttributeAsync("href");
                if (href is not null && href.StartsWith('/') && !href.Contains("/status/", StringComparison.Ordinal))
                {
                    return href.TrimStart('/');
                }
            }
        }
        catch (PlaywrightException)
        {
        }

        return null;
    }

    private static async Task<string?> ExtractTweetTextAsync(IPage page)
    {
        try
        {
            var el = await page.QuerySelectorAsync("article[data-testid='tweet'] div[data-testid='tweetText']");
            if (el is not null)
            {
                return await el.InnerTextAsync();
            }
        }
        catch (PlaywrightException)
        {
        }

        return null;
    }

    private static async Task<DateTime?> ExtractDateAsync(IPage page)
    {
        try
        {
            var timeElement = await page.QuerySelectorAsync("article[data-testid='tweet'] time");
            if (timeElement is not null)
            {
                var datetime = await timeElement.GetAttributeAsync("datetime");
                if (DateTime.TryParse(datetime, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                {
                    return date;
                }
            }
        }
        catch (PlaywrightException)
        {
        }

        return null;
    }

    private async Task<ScrapedMedia?> TakeScreenshotAsync(IPage page, string tweetId)
    {
        try
        {
            var tweetElement = await page.QuerySelectorAsync("article[data-testid='tweet']");
            byte[]? bytes;

            if (tweetElement is not null)
            {
                bytes = await tweetElement.ScreenshotAsync(new ElementHandleScreenshotOptions { Type = ScreenshotType.Png });
            }
            else
            {
                bytes = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png, FullPage = false });
            }

            if (bytes is null)
            {
                return null;
            }

            return new ScrapedMedia
            {
                Data = new ReadOnlyMemory<byte>(bytes),
                FileName = $"{tweetId}_screenshot.png",
                ContentType = "image/png",
                MediaType = ScrapedMediaType.Image,
            };
        }
        catch (PlaywrightException ex)
        {
            _logger.LogWarning(ScreenshotFailedEvent, ex, "Failed to take screenshot for tweet {TweetId}", tweetId);
            return null;
        }
    }

    private async Task<List<ScrapedMedia>> DownloadMediaAsync(IPage page, string tweetId, List<string> capturedVideoUrls, CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient("TweetMediaDownloader");

        var imageTask = DownloadImagesAsync(page, tweetId, httpClient, ct);
        var videoTask = DownloadVideosAsync(tweetId, capturedVideoUrls, httpClient, ct);

        try
        {
            await Task.WhenAll(imageTask, videoTask);
        }
        finally
        {
            httpClient.Dispose();
        }

        var media = new List<ScrapedMedia>();
        media.AddRange(await imageTask);
        media.AddRange(await videoTask);

        return media;
    }

    private async Task<List<ScrapedMedia>> DownloadImagesAsync(IPage page, string tweetId, HttpClient httpClient, CancellationToken ct)
    {
        var media = new List<ScrapedMedia>();
        var downloadedUrls = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            var selectors = new[]
            {
                "article[data-testid='tweet'] img[src*='pbs.twimg.com/media']",
                "article[data-testid='tweet'] img[src*='twimg.com/media']",
                "article[data-testid='tweet'] div[data-testid='tweetPhoto'] img",
                "article[data-testid='tweet'] a[href*='/photo/'] img",
            };

            var downloadTasks = new List<Task<ScrapedMedia?>>();
            var index = 0;

            foreach (var selector in selectors)
            {
                var imageElements = await page.QuerySelectorAllAsync(selector);
                foreach (var img in imageElements)
                {
                    var src = await img.GetAttributeAsync("src");
                    if (string.IsNullOrEmpty(src) || src.Contains("profile_images", StringComparison.OrdinalIgnoreCase) || src.Contains("emoji", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var highQualitySrc = GetHighQualityImageUrl(src);
                    var baseUrl = highQualitySrc.Split('?')[0];
                    if (!downloadedUrls.Add(baseUrl))
                    {
                        continue;
                    }

                    var extension = GetImageExtension(highQualitySrc);
                    var contentType = GetImageContentType(extension);
                    var fileName = $"{tweetId}_image_{index}{extension}";

                    downloadTasks.Add(DownloadMediaFileAsync(httpClient, highQualitySrc, fileName, contentType, ScrapedMediaType.Image, ct));
                    index++;
                }
            }

            var results = await Task.WhenAll(downloadTasks);
            media.AddRange(results.Where(m => m is not null)!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ImageDownloadErrorEvent, ex, "Error downloading images for tweet {TweetId}", tweetId);
        }

        return media;
    }

    private async Task<List<ScrapedMedia>> DownloadVideosAsync(string tweetId, List<string> capturedVideoUrls, HttpClient httpClient, CancellationToken ct)
    {
        var media = new List<ScrapedMedia>();

        try
        {
            var mp4Urls = capturedVideoUrls
                .Where(u => u.Contains(".mp4", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (mp4Urls.Count == 0)
            {
                return media;
            }

            var bestUrl = mp4Urls
                .OrderByDescending(GetVideoResolution)
                .First();

            var result = await DownloadMediaFileAsync(httpClient, bestUrl, $"{tweetId}_video.mp4", "video/mp4", ScrapedMediaType.Video, ct);
            if (result is not null)
            {
                media.Add(result);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(VideoDownloadErrorEvent, ex, "Error downloading videos for tweet {TweetId}", tweetId);
        }

        return media;
    }

    private static async Task<ScrapedMedia?> DownloadMediaFileAsync(HttpClient httpClient, string url, string fileName, string contentType, ScrapedMediaType mediaType, CancellationToken ct)
    {
        try
        {
            var bytes = await httpClient.GetByteArrayAsync(new Uri(url), ct);
            return new ScrapedMedia
            {
                Data = new ReadOnlyMemory<byte>(bytes),
                FileName = fileName,
                ContentType = contentType,
                MediaType = mediaType,
            };
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string GetHighQualityImageUrl(string url)
    {
        if (url.Contains("twimg.com/media", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = url.Split('?')[0];
            return $"{baseUrl}?format=jpg&name=4096x4096";
        }

        return url;
    }

    private static string GetImageExtension(string url)
    {
        if (url.Contains("format=png", StringComparison.OrdinalIgnoreCase))
        {
            return ".png";
        }

        if (url.Contains("format=gif", StringComparison.OrdinalIgnoreCase))
        {
            return ".gif";
        }

        if (url.Contains("format=webp", StringComparison.OrdinalIgnoreCase))
        {
            return ".webp";
        }

        return ".jpg";
    }

    private static string GetImageContentType(string extension)
    {
        return extension switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
    }

    private static int GetVideoResolution(string url)
    {
        var match = VideoResolutionRegex().Match(url);
        if (match.Success)
        {
            var width = int.Parse(match.Groups["width"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var height = int.Parse(match.Groups["height"].Value, System.Globalization.CultureInfo.InvariantCulture);
            return width * height;
        }

        if (url.Contains("1080", StringComparison.Ordinal))
        {
            return 1920 * 1080;
        }

        if (url.Contains("720", StringComparison.Ordinal))
        {
            return 1280 * 720;
        }

        if (url.Contains("480", StringComparison.Ordinal))
        {
            return 854 * 480;
        }

        return 0;
    }

    [GeneratedRegex(@"/(?<width>\d+)x(?<height>\d+)/", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VideoResolutionRegex();

    [GeneratedRegex(@"profile_images/(?<id>\d+)/", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProfileImageIdRegex();

    [GeneratedRegex(@"""user_id_str""\s*:\s*""(?<id>\d+)""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserIdStrRegex();
}
