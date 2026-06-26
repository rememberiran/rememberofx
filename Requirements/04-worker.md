# Memory of X — Scrape Worker Service

> **Scope:** Worker Service polling loop, scrape job flow, Playwright configuration, error handling, retry logic, and worker-specific configuration.
> **Read alongside:** `00-overview.md`, `01-database.md`, `06-observability.md`
> **Technology:** .NET Worker Service, Microsoft Playwright for .NET, Azure Storage Queue, Azure Blob Storage

---

## 1. Overview

The Worker is a separate .NET Worker Service project (`src/Worker`) deployed as its own ACA container (`ca-mox-worker-prod`). It has **no HTTP endpoints**. Its sole job is to dequeue `ScrapeJobMessage` messages from the `scrape-jobs` Azure Storage Queue, scrape tweet content via Playwright, and **persist all results to the database**. The API does not write tweet records — it only enqueues and writes an audit log (see `02-backend-api.md §4`).

Locally (F5), the Worker runs as a third startup project alongside the API and Frontend. It uses the same `DefaultAzureCredential` pattern — resolves to VS/Azure CLI credentials locally, Managed Identity in production.

**Before first local run,** create the dev queue once:
```bash
az storage queue create --name scrape-jobs \
  --account-name stmoxdev --auth-mode login
```

---

## 2. Project Structure

```
src/Worker/
  Worker.cs                      # IHostedService — main polling loop
  ScrapeJob.cs                   # Dequeues one message, orchestrates the full scrape
  Services/
    PlaywrightScrapeService.cs   # Headless Chromium: navigate, extract, screenshot
    BlobStorageService.cs        # Upload screenshot PNG to Azure Blob Storage
  appsettings.json
  appsettings.Development.json
  appsettings.Production.json
```

---

## 3. Polling Loop

```csharp
// Worker.cs
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var message = await _queue.DequeueAsync(
            visibilityTimeout: TimeSpan.FromMinutes(5));

        if (message is null)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            continue;
        }

        await _scrapeJob.ExecuteAsync(message, stoppingToken);
    }
}
```

- **Visibility timeout: 5 minutes** — the message is hidden from other consumers while being processed. If the worker crashes mid-job, the message reappears after 5 minutes and is retried automatically.
- **Idle backoff: 5 seconds** — avoids busy-polling when the queue is empty.
- **Concurrency: 1 message per replica.** ACA scales to up to 2 replicas based on queue depth (trigger: queue length > 5), giving max 2 concurrent scrapes.

---

## 4. Scrape Job Flow

All logic in `ScrapeJob.ExecuteAsync`. The Worker is responsible for **all database writes** — the API only enqueues a message and writes an audit log (see `02-backend-api.md §4`).

Each message contains: `{ xTweetUrl, xTweetId, authorXUsername, folderIds, submittedByUserId, submittedByIp, correlationId }`.

1. Open logger scope `{ CorrelationId, XTweetId }` — all worker logs carry these fields.
2. **Duplicate check:** query `Tweets` by `XTweetId`. If a row already exists, log `Info` ("duplicate — already processed"), delete message, return. (Handles the case where the API's dedup check passed but the message was enqueued twice.)
3. Check `message.DequeueCount >= 3` — if so, skip scraping and go directly to the failure path (see §6): insert `Tweet` row with `FetchStatus='ScrapeFailed'`, audit, delete message, return.
4. Launch Playwright headless Chromium (see §5 for required flags).
5. Navigate to `xTweetUrl` **without authentication** — public page, no cookies, no session.
6. Wait for CSS selector `article[data-testid="tweet"]` to render; timeout: 20 seconds.
7. Extract from DOM:
   - `TweetText` — inner text of the tweet body element
   - `TweetDate` — `datetime` attribute of the `<time>` element
   - `AuthorXUsername` — `@handle` from the author link
   - `AuthorXUserId` — parse from profile link URL (e.g. `/i/user/{id}`) or data attributes
   - `Tags` — all `#hashtag` anchor links in the tweet body → JSON array
   - **Media URLs** — all `<img>` sources within the tweet media container (images) and `<video>` sources (videos). Collect original URLs and media type (`Image` or `Video`) in order of appearance.
8. Capture PNG screenshot of the `<article>` element only (not full page): `await element.ScreenshotAsync()`.
9. Upload PNG to Azure Blob Storage — container: `screenshots`, blob name: `{XTweetId}.png`. If upload fails: log `Error`, set `ScreenshotBlobName = null`, continue (see §6.2).
10. **Download and upload media files** — for each media URL extracted in step 7:
    - Download the media file from the original URL.
    - Upload to Azure Blob Storage — container: `media`, blob name: `{XTweetId}_{orderIndex}.{ext}` (e.g., `123456_0.jpg`, `123456_1.mp4`).
    - If download or upload fails for a specific media item: log `Error`, set `BlobName = null` for that item, continue — partial media failure does not block archival.
    - Insert a `TweetMedia` row per media item with `MediaType`, `BlobName`, `OriginalUrl`, and `OrderIndex`.
11. **SQL transaction (see §7):**
    - **Insert `Tweet` row** with all scraped data, `FetchStatus='Ok'`, and submission metadata from the queue message (`SubmittedByUserId`, `SubmittedByIp`).
    - **Insert `TweetMedia` rows** for each media item extracted and downloaded (see step 10).
    - **Upsert `XUserProfiles`** using `AuthorXUserId` from scrape:
      - No row exists → insert stub: `XUserId`, `ScrapedUsername`, `CustomName = NULL`, `CreatedByUserId = NULL`
      - Row exists → update `ScrapedUsername` (handle may have changed)
    - **Insert `FolderTweet` rows** if `folderIds` were provided in the message.
    - **Insert `AuditLog`** row: `Action='Tweet.Scraped'`.
12. Delete message from queue (signals successful processing).

---

## 5. Playwright Configuration

### Required Chromium Launch Flags

```csharp
// PlaywrightScrapeService.cs
var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true,
    Args = new[]
    {
        "--no-sandbox",            // required: ACA containers cannot use kernel namespaces
        "--disable-dev-shm-usage"  // required: ACA /dev/shm is too small for Chromium
    }
});
```

Without `--no-sandbox`, Chromium will fail to start in a container. Without `--disable-dev-shm-usage`, Chromium crashes on pages with significant rendering work.

### Public Access — No Login

Tweets on X.com are publicly visible without a session for non-protected accounts. The worker navigates without any cookies or session state.

- User-Agent: set to a recent Chrome string to reduce bot-detection risk.
- No JavaScript injection, no cookie manipulation.
- If X starts requiring login to view tweets, `article[data-testid="tweet"]` will not be found → scraping fails to `ScrapeFailed`. This surfaces via the `screenshot.failure` alert in `06-observability.md §4`.

### Playwright Installation in Docker

```dockerfile
# Worker Dockerfile — install Playwright browsers at build time
RUN dotnet tool install --global Microsoft.Playwright.CLI && \
    /root/.dotnet/tools/playwright install chromium
```

See `07-infrastructure.md` for the full Worker Dockerfile.

---

## 6. Error Handling & Retry

| Condition | Behaviour |
|---|---|
| Deleted / unavailable tweet (`"This tweet is unavailable"` tombstone) | Insert `Tweet` with `FetchStatus='NotFound'`, audit `Tweet.NotFound`, delete message — **no retry** |
| Protected account (`"These tweets are from a protected account"`) | Insert `Tweet` with `FetchStatus='Private'`, audit `Tweet.Private`, delete message — **no retry** |
| Playwright navigation timeout (20s) | Do NOT delete message — let visibility timeout expire → automatic retry |
| DOM extraction fails (element not found) | Same as timeout — retry via visibility timeout |
| `DequeueCount >= 3` | Insert `Tweet` with `FetchStatus='ScrapeFailed'`, `ScrapeError=<message>`, audit `Tweet.ScrapeFailed`, delete message |
| Unhandled exception | Log `Error`, do NOT delete message — automatic retry via visibility timeout |

### Retry via DequeueCount

```csharp
if (message.DequeueCount >= 3)
{
    await InsertScrapeFailedAsync(message, "Max retries exceeded");
    await _queue.DeleteMessageAsync(message);
    return;
}
```

### Blob Upload Failure

If Azure Blob Storage is unreachable during screenshot upload:
- Catch the exception in `BlobStorageService.UploadAsync`
- Log `Error` with `CorrelationId`; increment `screenshot.failure` OTel counter
- Set `ScreenshotBlobName = null` — tweet text and metadata are still archived
- Continue to the SQL transaction — blob failure does not block archival

### Media Download/Upload Failure

Each media item is downloaded and uploaded independently. If a specific media item fails:
- Log `Error` with `CorrelationId` and media index; increment `media.failure` OTel counter
- Set `BlobName = null` for that `TweetMedia` row — `OriginalUrl` is still preserved for retry or manual download
- Continue processing remaining media items — partial failure does not block archival

---

## 7. Worker Transaction Boundary

The Worker creates all records in a single transaction. No tweet row exists before this point — the API only wrote an audit log.

### Success Path (scrape succeeded)

```
BEGIN TRANSACTION
  INSERT INTO Tweets
    (Id, XTweetId, XTweetUrl, FetchStatus='Ok', AuthorXUserId, AuthorXUsername,
     TweetText, TweetDate, Tags, ScreenshotBlobName, ScrapedAt,
     ScrapeAttempts, SubmittedByUserId, SubmittedByIp, CreatedAt)

  INSERT INTO TweetMedia (Id, TweetId, MediaType, BlobName, OriginalUrl, OrderIndex, CreatedAt)
    -- for each media item extracted from the tweet (images and videos)

  INSERT OR UPDATE XUserProfiles (XUserId, ScrapedUsername, ...)
    -- upsert: create stub if absent, update ScrapedUsername if present

  INSERT INTO FolderTweets (FolderId, TweetId, AddedByUserId, AddedAt)
    -- for each folderId in the queue message (if any)

  INSERT INTO AuditLog (Action='Tweet.Scraped', ...)
COMMIT

-- After commit only:
queue.DeleteMessage(message)
```

### Failure Path (NotFound / Private / ScrapeFailed)

```
BEGIN TRANSACTION
  INSERT INTO Tweets
    (Id, XTweetId, XTweetUrl, FetchStatus=@status, ScrapeError=@error,
     ScrapeAttempts, SubmittedByUserId, SubmittedByIp, CreatedAt)
    -- scraped columns remain NULL; no FolderTweet rows inserted

  INSERT INTO AuditLog (Action='Tweet.NotFound' | 'Tweet.Private' | 'Tweet.ScrapeFailed', ...)
COMMIT

-- After commit only:
queue.DeleteMessage(message)
```

Blob upload happens before the transaction opens. An orphaned blob (upload succeeded, SQL failed) is acceptable — blob name is deterministic (`{XTweetId}.png`) so a retry will overwrite it.

---

## 8. Configuration

### `appsettings.Development.json` (committed, non-sensitive)

```json
{
  "ASPNETCORE_ENVIRONMENT": "Development",
  "KeyVault": { "Uri": "https://kv-mox-dev.vault.azure.net/" },
  "AzurePrimaryRegion": "eastus",
  "BlobStorage": { "AccountUrl": "https://stmoxdev.blob.core.windows.net/" },
  "Queue": {
    "AccountUrl": "https://stmoxdev.queue.core.windows.net/",
    "QueueName": "scrape-jobs"
  },
  "Playwright": {
    "NavigationTimeoutMs": 20000,
    "MaxDequeueCount": 3
  },
  "Otel": { "UseConsoleExporter": true }
}
```

### Production Environment Variables (via ACA Key Vault references)

| Variable | Key Vault Secret |
|---|---|
| `ConnectionStrings__Default` | `db-connection-string` |
| `BlobStorage__AccountUrl` | `blob-storage-url` |
| `Queue__AccountUrl` | `queue-storage-url` |
| `Queue__QueueName` | (literal — `scrape-jobs`) |
| `ApplicationInsights__ConnectionString` | `app-insights-connection-string` |
| `AZURE_PRIMARY_REGION` | (literal env var) |

### ACA Resource Allocation

The Worker requires more resources than the other services because Chromium is memory-hungry:

- **CPU:** 1.0 vCPU
- **Memory:** 2Gi

The default 0.5 vCPU / 1Gi will cause Chromium to crash under normal rendering load.

**Scale rule:** Azure Storage Queue — scale up when `scrape-jobs` queue length > 5; scale to zero when queue is empty.
