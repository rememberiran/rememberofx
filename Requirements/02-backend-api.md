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

## 2. Result Pattern — No Exceptions for Control Flow

All service methods return a **`Result<T>`** (or `Result` for void operations) instead of throwing exceptions. Exceptions are reserved exclusively for truly unexpected failures (e.g., null references, configuration errors, infrastructure faults).

- Services return `Result.Success(value)` or `Result.Failure(error)` with a typed error.
- Controllers map `Result` outcomes to HTTP responses — success to `200`/`201`/`202`, failure to `400`/`404`/`409` etc.
- No `try/catch` in controllers for business logic errors — the `Result` type carries error information explicitly.
- Infrastructure failures (DB unavailable, queue timeout) may still throw and are caught by global exception-handling middleware, which returns `500`.

```csharp
// Service returns Result
public async Task<Result<TweetDto>> GetByIdAsync(Guid id, CancellationToken ct)
{
    var tweet = await _db.Tweets.FindAsync(id, ct);
    if (tweet is null)
        return Result.Failure<TweetDto>(DomainError.NotFound("Tweet not found"));

    return Result.Success(MapToDto(tweet));
}

// Controller maps Result to HTTP
var result = await _tweetQueryService.GetByIdAsync(id, ct);
return result.IsSuccess
    ? Ok(result.Value)
    : result.ToActionResult();
```

---

## 3. Project Structure

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
      Seed/
        DatabaseSeeder.cs
    Queue/
      ScrapeQueueService.cs        # Wraps Azure Storage Queue
      ScrapeJobMessage.cs          # { XTweetUrl, XTweetId, AuthorXUsername, FolderIds, SubmittedByUserId, SubmittedByIp, CorrelationId }
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

## 4. API Endpoints

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
| `POST` | `/api/auth/token` | Exchange X access token for application JWT — see §9 |
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

---

## 5. Tweet Submission Flow

All logic is in `TweetSubmissionService`. The controller only maps HTTP request → command → HTTP response.

The API does **not** create a `Tweet` row, `XUserProfile` stub, or `FolderTweet` rows. It only writes an audit log and enqueues the full submission details. The Worker is responsible for all database writes after scraping (see `04-worker.md §4`).

1. Parse `XTweetId` from URL via regex `/status/(\d+)/` — return `400` if no match. Also parse `AuthorXUsername` from URL path (`https://x.com/{username}/status/{id}`).
2. **Duplicate check:** query `Tweets` by `XTweetId`. If found, return `409 Conflict` with `{ tweetId, fetchStatus, message: "Already submitted" }`.
3. Insert `AuditLog` row: `Action='Tweet.SubmitRequest'`, `Payload` = JSON of `{ xTweetUrl, xTweetId, authorXUsername, folderIds, submittedByUserId, submittedByIp }`.
4. Enqueue `ScrapeJobMessage` to `scrape-jobs` queue with all submission details (see below).
5. Return `202 Accepted`.

> **If enqueue fails after audit log:** no tweet row exists, but the audit log records the intent. Log `Warning`. The user can re-submit since there is no `Tweet` row to trigger the duplicate check.

### ScrapeJobMessage

The queue message carries everything the Worker needs to create the tweet and related records:

```json
{
  "xTweetUrl": "https://x.com/user/status/123456",
  "xTweetId": "123456",
  "authorXUsername": "user",
  "folderIds": ["guid1", "guid2"],
  "submittedByUserId": "guid-or-null",
  "submittedByIp": "1.2.3.4",
  "correlationId": "uuid"
}
```

---

## 6. Status Polling Endpoint

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

## 7. Search Behaviour

- Returns only tweets with `FetchStatus = 'Ok'`
- At least one of `q`, `tag`, `username`, or `userId` required — returns `400` if all absent
- Multiple params combine with **AND** logic

| Param | Strategy |
|---|---|
| `q` | Full-text search on `TweetText`, `XUserProfiles.CustomName`, and `XUserProfiles.Description` — supports both **English** and **Farsi** (see multilingual setup below) |
| `tag` | Exact tag match within `Tags` JSON array — e.g. `tag=freedom` matches `["freedom","iran"]` |
| `username` | Case-insensitive `LIKE '%' + @username + '%'` on `AuthorXUsername` |
| `userId` | Exact match on `AuthorXUserId` |

### Multilingual Full-Text Search (English + Farsi)

SQL Server full-text search must support both English and Farsi keywords across `TweetText`, `XUserProfiles.CustomName`, and `XUserProfiles.Description`:

1. **Full-text catalog:** create a single `FT_MemoryOfX` catalog shared by both tables.
2. **Full-text indexes:** use `LANGUAGE 0` (Neutral) so the word breaker handles both Latin and Arabic-script tokens without requiring a language column or dual-index.
3. **Query:** when `q` is provided, search across both tables using `CONTAINS` and combine results — tweets match directly on text, or via a `JOIN` to `XUserProfiles` on `AuthorXUserId = XUserId` where the profile's `CustomName` or `Description` matches.
4. **Tags search** uses `OPENJSON(Tags)` with an exact-match filter rather than full-text — tags are structured data, not free text.

> **Setup migration:** the EF migration must create the full-text catalog and indexes via raw SQL (`migrationBuilder.Sql(...)`), since EF Core has no first-class full-text support.

```sql
-- Migration: CreateFullTextIndexes
CREATE FULLTEXT CATALOG FT_MemoryOfX AS DEFAULT;

CREATE FULLTEXT INDEX ON Tweets(TweetText LANGUAGE 0)
  KEY INDEX PK_Tweets
  WITH STOPLIST = OFF;

CREATE FULLTEXT INDEX ON XUserProfiles(CustomName LANGUAGE 0, Description LANGUAGE 0)
  KEY INDEX PK_XUserProfiles
  WITH STOPLIST = OFF;
```

`STOPLIST = OFF` is intentional — the default English stoplist drops common words that may be meaningful in Farsi search contexts.

**Search query logic for `q` parameter:**
```sql
SELECT DISTINCT t.*
FROM Tweets t
LEFT JOIN XUserProfiles p ON t.AuthorXUserId = p.XUserId
WHERE t.FetchStatus = 'Ok'
  AND (
    CONTAINS(t.TweetText, @q)
    OR CONTAINS(p.CustomName, @q)
    OR CONTAINS(p.Description, @q)
  )
```

Sorting:
- `sort=votes` (default): `ORDER BY VoteCount DESC, CreatedAt DESC`
- `sort=date`: `ORDER BY CreatedAt DESC`

Each result includes the author's `XUserProfile` as a nested `authorProfile` field (`{ customName, description }` or `null`).

When searching by `userId`, if an `XUserProfile` exists, it is also returned as a top-level `subjectProfile` field above the paginated tweet list.

Zero results: `200 OK` with `{ items: [], totalCount: 0 }`.

---

## 8. Voting — Deduplication Strategy

Handled entirely in `VoteService`. Logic:

- **Authenticated users:** deduped by `(TweetId, VoterUserId)` unique constraint — one vote per account regardless of IP.
- **Anonymous users:** deduped by `(TweetId, VoterIp)` — best-effort; IP abuse is an accepted limitation.
- **Auth overrides anonymous:** if a logged-in user previously voted anonymously from the same IP, both rows are preserved but `VoteCount` is not incremented again (check before insert).
- On duplicate: return `409 Conflict` with `{ message: "Already voted" }`.

---

## 9. Authentication — Token Exchange

### 9.1 Auth Flow

The Frontend handles X OAuth 2.0 PKCE and obtains an X access token. It then sends that token to the Backend, which validates it, resolves the user, and issues an application JWT. The Backend is the **sole JWT issuer** — the Frontend never inspects or creates JWTs.

#### `POST /api/auth/token`

**Request:**
```json
{ "xAccessToken": "..." }
```

**Backend logic (`AuthService.ExchangeTokenAsync`):**

1. Call `GET https://api.twitter.com/2/users/me` with `Authorization: Bearer {xAccessToken}` to extract `id` and `username`.
2. If the X API call fails (invalid or expired token): return `401` with `{ message: "Invalid or expired X token" }`.
3. Look up `Users` table by `XUserId = id`.
4. If not found or `IsActive = false`: return `403` with `{ message: "Access denied — your X account is not registered" }`. Write `AuditLog` with `Action = 'Auth.Denied'`.
5. Issue a signed JWT with the claims listed below.
6. Write `AuditLog` with `Action = 'Auth.Login'`.
7. Return `200` with `{ token, expiresAt }`.

**Note:** The Backend calls the X API server-side using a plain `HttpClient` — no X OAuth client credentials needed. The user's access token is sufficient to call the `/2/users/me` endpoint.

### 9.2 JWT Specification

- **Algorithm:** HS256 (shared secret — Backend signs, Backend validates)
- **Issuer:** `Jwt:Issuer` config key (e.g., `https://memoryofx.com`)
- **Expiry:** 8 hours (`exp` claim)
- **Clock skew tolerance:** 2 minutes (ASP.NET Core default)

| Claim | Value |
|---|---|
| `sub` | `Users.XUserId` |
| `username` | `Users.XUsername` (from X API response) |
| `role` | `'Admin'` or `'Contributor'` (from `Users` table) |
| `jti` | `Guid.NewGuid()` |
| `iat` | Issued-at Unix timestamp |
| `exp` | Expiry Unix timestamp |

**Token expiry:** on `401` from any API call, the frontend clears the session cookie, sets `IsAuthenticated = false`, and redirects to `/?sessionExpired=true` with a banner. No silent refresh — the user must log in again via X SSO.

**Mid-session deactivation:** the backend checks `IsActive` on every protected request against the `Users` table (not just at login). A deactivated user is rejected immediately on their next API call even within the 8h token window.

---

## 10. Configuration

### `appsettings.Development.json` (committed, non-sensitive)

```json
{
  "ASPNETCORE_ENVIRONMENT": "Development",
  "KeyVault": { "Uri": "https://kv-mox-dev.vault.azure.net/" },
  "AzurePrimaryRegion": "eastus",
  "BlobStorage": { "AccountUrl": "https://stmoxdev.blob.core.windows.net/" },
  "Queue": { "AccountUrl": "https://stmoxdev.queue.core.windows.net/", "QueueName": "scrape-jobs" },
  "Jwt": { "Issuer": "https://localhost:5000", "ExpiryHours": 8 },
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
| `ApplicationInsights__ConnectionString` | `app-insights-connection-string` |
| `Seed__AdminXUserId` | `seed-admin-x-user-id` |
| `Seed__AdminXUsername` | `seed-admin-x-username` |
| `AZURE_PRIMARY_REGION` | (literal env var) |
