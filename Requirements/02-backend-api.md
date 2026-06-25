# Memory of X — Backend API

> **Scope:** Project structure, API endpoints, business logic flows, JWT spec, voting strategy, and transaction boundaries.
> **Read alongside:** `00-overview.md`, `01-database.md`, `03-middleware.md`, `06-observability.md`
> **Technology:** ASP.NET Core 8 MVC, C# 12, EF Core 8

---

## 1. Architecture Principle — Thin Controllers

Controllers handle only HTTP concerns: model binding, validation, mapping to service inputs, and translating service results to HTTP responses. **All business logic lives in `Application/Services`.**

```csharp
// TweetsController.cs — example of the required pattern
[HttpPost]
public async Task<IActionResult> Submit(
    [FromBody] SubmitTweetRequest request,
    CancellationToken ct)
{
    var command = new SubmitTweetCommand(
        TweetUrl: request.TweetUrl,
        FolderIds: request.FolderIds,
        SubmittedByIp: HttpContext.GetClientIp(),
        SubmittedByUserId: User.GetUserId());  // null for anonymous

    var result = await _tweetSubmissionService.SubmitAsync(command, ct);

    return result.IsSuccess
        ? Accepted(new SubmitTweetResponse(result.TweetId, result.FetchStatus))
        : result.ToActionResult();  // maps domain errors to 400/409/etc.
}
```

No EF queries, no branching logic, no direct infrastructure calls in controllers.

---

## 2. Project Structure

```
src/
  Api/
    Controllers/
      TweetsController.cs          # → TweetSubmissionService / TweetQueryService
      FoldersController.cs         # → FolderService
      VotesController.cs           # → VoteService
      XUserProfilesController.cs   # → XUserProfileService
      AdminController.cs           # → UserService
      AuthController.cs            # → AuthService
    Middleware/
      CorrelationIdMiddleware.cs
      HttpLoggingMiddleware.cs      # See 03-middleware.md
      LoadSheddingMiddleware.cs     # See 03-middleware.md
    Models/
      Requests/                    # Input DTOs — validated with DataAnnotations or FluentValidation
      Responses/                   # Output DTOs — mapped from domain entities by services
    Extensions/
      ServiceCollectionExtensions.cs
      ApplicationBuilderExtensions.cs

  Application/
    Services/
      TweetSubmissionService.cs    # URL parse, dedup, stub insert, XUserProfile stub, enqueue
      TweetQueryService.cs         # Search, get-by-id, status polling
      FolderService.cs             # CRUD, depth validation, ancestor traversal, FolderTweet management
      VoteService.cs               # Dedup strategy, VoteCount transaction
      XUserProfileService.cs       # Upsert profile; stub creation on tweet submission
      UserService.cs               # Admin user CRUD, IsActive check
      AuthService.cs               # JWT generation, X SSO verification
    Interfaces/
      ITweetSubmissionService.cs
      ITweetQueryService.cs
      IFolderService.cs
      IVoteService.cs
      IXUserProfileService.cs
      IUserService.cs
      IAuthService.cs
    Models/                        # Service-layer DTOs — decoupled from HTTP and EF models
      TweetDto.cs
      FolderDto.cs
      XUserProfileDto.cs
      SubmitTweetCommand.cs
      SearchTweetsQuery.cs

  Infrastructure/
    Data/
      AppDbContext.cs
      Migrations/
      Repositories/
      Seed/
        DatabaseSeeder.cs
    Queue/
      ScrapeQueueService.cs        # Wraps Azure Storage Queue
      ScrapeJobMessage.cs          # { TweetId, XTweetUrl, CorrelationId }
    BlobStorage/
      BlobStorageService.cs

  Domain/
    Entities/
    Enums/
      FetchStatus.cs               # Pending, Processing, Ok, NotFound, Private, ScrapeFailed
    Exceptions/

  appsettings.json
  appsettings.Development.json     # Committed — non-sensitive dev config only
  appsettings.Production.json      # Committed — Key Vault URI only; no real secrets
```

---

## 3. API Endpoints

### Public (no auth required)

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/tweets` | Submit a tweet URL — returns `202 Accepted` with `{ tweetId, fetchStatus: "Pending" }` |
| `GET` | `/api/tweets/{id}/status` | Poll scrape status — `{ tweetId, fetchStatus, tweetData? }` |
| `GET` | `/api/tweets/search` | Search: `?q=` `&username=` `&userId=` `&sort=votes\|date` `&page=1` `&pageSize=20` |
| `GET` | `/api/tweets/{id}` | Get single archived tweet |
| `GET` | `/api/folders` | List active root folders (`ParentFolderId IS NULL`) with `childCount` |
| `GET` | `/api/folders/{id}` | Single folder — includes `parentFolderId`, `children[]`, breadcrumb chain |
| `GET` | `/api/folders/{id}/children` | Immediate active children of a folder |
| `GET` | `/api/folders/{id}/tweets` | Tweets in a folder; `?sort=votes\|date` — `FetchStatus='Ok'` only |
| `GET` | `/api/xusers/{xUserId}` | Get X user profile — `200` for stub or full; `404` if no tweets ever submitted |
| `POST` | `/api/votes/{tweetId}` | Cast a vote |
| `GET` | `/health/live` | Liveness probe |
| `GET` | `/health/ready` | Readiness — checks DB and queue connectivity |

### Contributor (requires `Contributor` or `Admin` role)

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/tweets` | Submit tweet + optional `folderIds` |
| `POST` | `/api/folders` | Create folder — `{ name, description?, parentFolderId? }` |
| `PUT` | `/api/folders/{id}` | Update name, description, or `parentFolderId` |
| `POST` | `/api/folders/{folderId}/tweets/{tweetId}` | Add tweet to folder |
| `DELETE` | `/api/folders/{folderId}/tweets/{tweetId}` | Remove tweet from folder |
| `PUT` | `/api/xusers/{xUserId}` | Upsert profile — `{ customName?, description? }` — at least one field required. Returns `200` with updated profile. Creates stub if absent. |
| `GET` | `/api/xusers/{xUserId}` | Full profile including `scrapedUsername` |

### Admin (requires `Admin` role)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/admin/users` | List all authorized users |
| `POST` | `/api/admin/users` | Add user — `{ xUserId, role }` |
| `PUT` | `/api/admin/users/{id}` | Update role or `IsActive` |
| `DELETE` | `/api/admin/users/{id}` | Deactivate (soft delete) |
| `GET` | `/api/auth/verify` | `?xUserId=xxx` — verify user is active, return signed JWT |

---

## 4. Tweet Submission Flow

All logic is in `TweetSubmissionService`. The controller only maps HTTP request → command → HTTP response.

1. Parse `XTweetId` from URL via regex `/status/(\d+)/` — return `400` if no match. Also parse `AuthorXUsername` from URL path (`https://x.com/{username}/status/{id}`).
2. **Duplicate check:** query `Tweets` by `XTweetId`. If found, return `409 Conflict` with `{ tweetId, fetchStatus, message: "Already submitted" }`.
3. Begin SQL transaction:
   - Insert stub `Tweet` row: `FetchStatus='Pending'`, all scraped columns null.
   - **Auto-create `XUserProfiles` stub:** if no row exists for this `XUserId`, insert stub with `ScrapedUsername` from URL parse, `CustomName = NULL`, `CreatedByUserId = NULL`. No-op if profile exists.
   - If authenticated Contributor and `folderIds` provided: insert `FolderTweet` rows.
   - Insert `AuditLog` row: `Action='Tweet.Submit'`.
4. Commit transaction.
5. **After commit:** enqueue `ScrapeJobMessage` to `scrape-jobs` queue: `{ tweetId, xTweetUrl, correlationId }`.
6. Return `202 Accepted` with `{ tweetId, fetchStatus: "Pending" }`.

> **If enqueue fails after commit:** the tweet row exists with `FetchStatus='Pending'` but no job is in the queue. Log `Warning`. Any tweet stuck in `Pending` for > 10 minutes can be re-enqueued via admin action or a reconciliation job.

### Submission Transaction Boundary

```
BEGIN TRANSACTION
  INSERT INTO Tweets (Id, XTweetId, XTweetUrl, FetchStatus='Pending', SubmittedByIp, ...)
  INSERT OR IGNORE INTO XUserProfiles (XUserId, ScrapedUsername, CreatedAt, ...)
  INSERT INTO FolderTweets (...)    -- if folderIds provided
  INSERT INTO AuditLog (Action='Tweet.Submit', ...)
COMMIT
-- After commit only: enqueue to Azure Storage Queue
```

---

## 5. Status Polling Endpoint

`GET /api/tweets/{id}/status`

```json
{
  "tweetId": "...",
  "fetchStatus": "Pending | Processing | Ok | NotFound | Private | ScrapeFailed",
  "tweetData": null
}
```

When `fetchStatus = 'Ok'`, `tweetData` contains the full `TweetDto` (text, author, date, tags, screenshotUrl — SAS URL generated at this point).

When `fetchStatus` is `NotFound`, `Private`, or `ScrapeFailed`, `tweetData` contains `{ fetchStatus, xTweetUrl }` for the frontend to show the appropriate badge.

The frontend polls every 3 seconds. Maximum poll duration: 2 minutes. See `05-frontend.md` for UI behaviour.

---

## 6. Search Behaviour

- Returns only tweets with `FetchStatus = 'Ok'`
- At least one of `q`, `username`, or `userId` required — returns `400` if all absent
- Multiple params combine with **AND** logic

| Param | Strategy |
|---|---|
| `q` | `CONTAINS(TweetText, @q)` via SQL Server full-text index; also searches `Tags` via `JSON_VALUE` |
| `username` | Case-insensitive `LIKE '%' + @username + '%'` on `AuthorXUsername` |
| `userId` | Exact match on `AuthorXUserId` |

Sorting:
- `sort=votes` (default): `ORDER BY VoteCount DESC, CreatedAt DESC`
- `sort=date`: `ORDER BY CreatedAt DESC`

Each result includes the author's `XUserProfile` as a nested `authorProfile` field (`{ customName, description }` or `null`).

When searching by `userId`, if an `XUserProfile` exists, it is also returned as a top-level `subjectProfile` field above the paginated tweet list.

Zero results: `200 OK` with `{ items: [], totalCount: 0 }`.

---

## 7. Voting — Deduplication Strategy

Handled entirely in `VoteService`. Logic:

- **Authenticated users:** deduped by `(TweetId, VoterUserId)` unique constraint — one vote per account regardless of IP.
- **Anonymous users:** deduped by `(TweetId, VoterIp)` — best-effort; IP abuse is an accepted limitation.
- **Auth overrides anonymous:** if a logged-in user previously voted anonymously from the same IP, both rows are preserved but `VoteCount` is not incremented again (check before insert).
- On duplicate: return `409 Conflict` with `{ message: "Already voted" }`.

---

## 8. JWT Specification

- **Algorithm:** HS256 (shared secret between Backend and Frontend)
- **Issuer:** `Jwt:Issuer` config key (e.g., `https://memoryofx.com`)
- **Expiry:** 8 hours (`exp` claim)
- **Clock skew tolerance:** 2 minutes (ASP.NET Core default)

| Claim | Value |
|---|---|
| `sub` | `Users.XUserId` |
| `username` | `Users.XUsername` |
| `role` | `'Admin'` or `'Contributor'` |
| `jti` | `Guid.NewGuid()` |
| `iat` | Issued-at Unix timestamp |
| `exp` | Expiry Unix timestamp |

**Token expiry:** on `401` from any API call, the frontend clears the session cookie, sets `IsAuthenticated = false`, and redirects to `/?sessionExpired=true` with a banner. No silent refresh — the user must log in again via X SSO.

**Mid-session deactivation:** the backend checks `IsActive` on every protected request against the `Users` table (not just at login). A deactivated user is rejected immediately on their next API call even within the 8h token window.

---

## 9. Configuration

### `appsettings.Development.json` (committed, non-sensitive)

```json
{
  "ASPNETCORE_ENVIRONMENT": "Development",
  "KeyVault": { "Uri": "https://kv-mox-dev.vault.azure.net/" },
  "AzurePrimaryRegion": "eastus",
  "BlobStorage": { "AccountUrl": "https://stmoxdev.blob.core.windows.net/" },
  "Queue": { "AccountUrl": "https://stmoxdev.queue.core.windows.net/", "QueueName": "scrape-jobs" },
  "Jwt": { "Issuer": "https://localhost:5000", "ExpiryHours": 8 },
  "RateLimit": { "UseRedis": false },
  "Otel": { "UseConsoleExporter": true },
  "Seed": { "AdminXUserId": "", "AdminXUsername": "" },
  "Folders": { "MaxPerContributor": 50, "MaxTweetsPerFolder": 1000, "MaxDepth": 5 }
}
```

### User Secrets (local only, never committed)

```json
{
  "ConnectionStrings": {
    "Default": "Server=sql-mox-dev.database.windows.net;Database=sqldb-mox-dev;Authentication=Active Directory Default;"
  },
  "Jwt": { "Secret": "local-dev-jwt-secret-min-32-chars" },
  "Seed": { "AdminXUserId": "123456789", "AdminXUsername": "admin_handle" }
}
```

### Production Environment Variables (via ACA Key Vault references)

| Variable | Key Vault Secret |
|---|---|
| `ConnectionStrings__Default` | `db-connection-string` |
| `BlobStorage__AccountUrl` | `blob-storage-url` |
| `Queue__AccountUrl` | `queue-storage-url` |
| `Queue__QueueName` | (literal — `scrape-jobs`) |
| `Jwt__Secret` | `jwt-secret` |
| `Redis__ConnectionString` | `redis-connection-string` |
| `ApplicationInsights__ConnectionString` | `app-insights-connection-string` |
| `Seed__AdminXUserId` | `seed-admin-x-user-id` |
| `Seed__AdminXUsername` | `seed-admin-x-username` |
| `AZURE_PRIMARY_REGION` | (literal env var) |
