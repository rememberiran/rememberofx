# Memory of X — Database

> **Scope:** All table schemas, indexes, constraints, seed logic, and data integrity rules.
> **Read alongside:** `00-overview.md`
> **Used by:** Backend API, Scrape Worker (both read/write this database)

---

## 1. Engine & Setup

- **SQL Server** on **Azure SQL Database** (separate dev and prod instances — see `00-overview.md §6.2`)
- **ORM:** EF Core 8, code-first migrations
- Migrations run on application startup via a hosted `IHostedService` — never in the request pipeline
- Connection string injected via env var `ConnectionStrings__Default` (from Key Vault in prod; from User Secrets or `appsettings.Development.json` locally)

---

## 2. Admin Bootstrap / Seed

The very first Admin cannot be added through the application (no admin exists yet). On first run, the migration hosted service checks whether the `Users` table is empty and seeds the initial Admin from configuration:

```json
{
  "Seed": {
    "AdminXUserId": "123456789",
    "AdminXUsername": "initial_admin_handle"
  }
}
```

The seed is idempotent — if a row with `XUserId = AdminXUserId` already exists, it is skipped. After the first admin is seeded, all subsequent admins are added via the admin UI.

---

## 3. Tables

### `Users`

Stores the application's authorized users (Contributors and Admins). This is separate from `XUserProfiles`, which stores metadata about the subjects being archived.

| Column | Type | Constraints |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` | PK, default `NEWID()` |
| `XUserId` | `NVARCHAR(50)` | UNIQUE, NOT NULL |
| `XUsername` | `NVARCHAR(100)` | NOT NULL |
| `Role` | `NVARCHAR(20)` | NOT NULL — `'Admin'` or `'Contributor'` |
| `IsActive` | `BIT` | NOT NULL, default `1` |
| `CreatedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |
| `CreatedByUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, nullable |

### `Tweets`

A row is inserted immediately on submission with `FetchStatus='Pending'` and all scraped columns null. The worker populates the nullable columns after scraping.

| Column | Type | Constraints |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` | PK, default `NEWID()` |
| `XTweetId` | `NVARCHAR(50)` | UNIQUE, NOT NULL |
| `XTweetUrl` | `NVARCHAR(500)` | NOT NULL |
| `AuthorXUserId` | `NVARCHAR(50)` | nullable — populated by worker |
| `AuthorXUsername` | `NVARCHAR(100)` | nullable — populated by worker |
| `TweetText` | `NVARCHAR(MAX)` | nullable — populated by worker |
| `TweetDate` | `DATETIME2` | nullable — populated by worker |
| `ScreenshotBlobName` | `NVARCHAR(200)` | nullable — blob name only (e.g. `{XTweetId}.png`); SAS URL generated at read time |
| `Tags` | `NVARCHAR(MAX)` | nullable — JSON array of hashtag strings |
| `VoteCount` | `INT` | NOT NULL, default `0` — denormalized counter kept in sync by application logic |
| `FetchStatus` | `NVARCHAR(20)` | NOT NULL — `'Pending'`, `'Processing'`, `'Ok'`, `'NotFound'`, `'Private'`, `'ScrapeFailed'` |
| `ScrapeAttempts` | `INT` | NOT NULL, default `0` |
| `ScrapeError` | `NVARCHAR(1000)` | nullable — last scrape error message |
| `SubmittedByUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, nullable (NULL = anonymous) |
| `SubmittedByIp` | `NVARCHAR(50)` | NOT NULL |
| `CreatedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |
| `ScrapedAt` | `DATETIME2` | nullable — set when worker completes scrape |

`ScreenshotBlobName` stores only the blob name, not a URL or SAS token. SAS URLs are generated on-the-fly at read time using a user-delegation SAS tied to Managed Identity — never stored in the database.

### `TweetMedia`

Stores references to downloaded media files (images and videos) attached to a tweet. Each media file is downloaded by the Worker and uploaded to the `media` blob container. A tweet can have zero or more media items.

| Column | Type | Constraints |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` | PK, default `NEWID()` |
| `TweetId` | `UNIQUEIDENTIFIER` | FK → `Tweets.Id` (CASCADE), NOT NULL |
| `MediaType` | `NVARCHAR(10)` | NOT NULL — `'Image'` or `'Video'` |
| `BlobName` | `NVARCHAR(200)` | nullable — blob name in `media` container (e.g. `{XTweetId}_0.jpg`); SAS URL generated at read time |
| `OriginalUrl` | `NVARCHAR(500)` | nullable — original media URL from the tweet |
| `OrderIndex` | `INT` | NOT NULL — ordering of media within the tweet (0-based) |
| `CreatedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |

`BlobName` stores only the blob name within the `media` container. SAS URLs are generated on-the-fly at read time, same as `ScreenshotBlobName`. `OriginalUrl` preserves the source URL for provenance tracking.

### `XUserProfiles`

Describes the *subjects* being archived — the people whose tweets are captured. Not to be confused with `Users`, which is the app's access roster.

A row is auto-created as a stub when a tweet is first submitted for a given `XUserId`. Contributors can enrich the stub with `CustomName` and `Description`.

| Column | Type | Constraints |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` | PK, default `NEWID()` |
| `XUserId` | `NVARCHAR(50)` | UNIQUE, NOT NULL |
| `ScrapedUsername` | `NVARCHAR(100)` | nullable — updated by worker on each successful scrape |
| `CustomName` | `NVARCHAR(200)` | nullable — contributor-provided; `NULL` until explicitly set |
| `Description` | `NVARCHAR(2000)` | nullable — free-text description |
| `CreatedByUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, nullable — `NULL` when auto-created |
| `UpdatedByUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, nullable |
| `CreatedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |
| `UpdatedAt` | `DATETIME2` | nullable |

### `Folders`

Supports nested folder trees via a self-referencing `ParentFolderId`. A `NULL` parent means root level.

| Column | Type | Constraints |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` | PK, default `NEWID()` |
| `ParentFolderId` | `UNIQUEIDENTIFIER` | FK → `Folders.Id`, nullable — `NULL` = root folder |
| `Name` | `NVARCHAR(200)` | NOT NULL |
| `Description` | `NVARCHAR(1000)` | nullable |
| `CreatedByUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, NOT NULL |
| `CreatedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |
| `IsActive` | `BIT` | NOT NULL, default `1` |

### `FolderTweets` (Many-to-Many)

| Column | Type | Constraints |
|---|---|---|
| `FolderId` | `UNIQUEIDENTIFIER` | FK → `Folders.Id`, NOT NULL |
| `TweetId` | `UNIQUEIDENTIFIER` | FK → `Tweets.Id`, NOT NULL |
| `AddedByUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, NOT NULL |
| `AddedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |
| **PK** | composite | `(FolderId, TweetId)` |

### `Votes`

| Column | Type | Constraints |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` | PK, default `NEWID()` |
| `TweetId` | `UNIQUEIDENTIFIER` | FK → `Tweets.Id`, NOT NULL |
| `VoterIp` | `NVARCHAR(50)` | NOT NULL |
| `VoterUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, nullable |
| `CreatedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |
| **UNIQUE** | | `(TweetId, VoterUserId)` where `VoterUserId IS NOT NULL` — authenticated dedup by identity |
| **UNIQUE** | | `(TweetId, VoterIp)` — anonymous dedup by IP |

### `AuditLog`

Write-only. All write interactions across the system are recorded here. Read interactions are tracked via OTel metrics only.

| Column | Type | Constraints |
|---|---|---|
| `Id` | `BIGINT` | PK, IDENTITY |
| `CorrelationId` | `NVARCHAR(36)` | NOT NULL — indexed |
| `Action` | `NVARCHAR(100)` | NOT NULL — e.g. `'Tweet.Submit'`, `'Folder.Create'` |
| `EntityType` | `NVARCHAR(50)` | NOT NULL |
| `EntityId` | `NVARCHAR(50)` | nullable |
| `PerformedByUserId` | `UNIQUEIDENTIFIER` | FK → `Users.Id`, nullable |
| `IpAddress` | `NVARCHAR(50)` | NOT NULL |
| `Region` | `NVARCHAR(100)` | nullable |
| `Payload` | `NVARCHAR(MAX)` | nullable — JSON snapshot of relevant inputs |
| `CreatedAt` | `DATETIME2` | NOT NULL, default `GETUTCDATE()` |

---

## 4. Indexes

| Index | Column(s) | Purpose |
|---|---|---|
| Unique | `Tweets.XTweetId` | Duplicate detection on submit |
| Non-clustered | `Tweets.AuthorXUserId` | Username/user-id search |
| Non-clustered | `Tweets.VoteCount DESC` | Vote-sorted queries |
| Full-text | `Tweets.TweetText` | Keyword search via `CONTAINS()` |
| Non-clustered | `Tweets.CreatedAt DESC` | Date-sorted queries |
| Non-clustered | `Tweets.FetchStatus` | Worker queries on `'Pending'` / `'Processing'` |
| Non-clustered | `Folders.ParentFolderId` | Fetching children of a folder |
| Unique | `XUserProfiles.XUserId` | Profile lookup by X user ID |
| Non-clustered | `TweetMedia.TweetId` | Fetch media for a tweet |
| Non-clustered | `AuditLog.CorrelationId` | Log tracing by correlation ID |

---

## 5. Data Rules

### 5.1 VoteCount Consistency

`VoteCount` is a denormalized counter kept in sync by the application layer (not a trigger). The vote service uses a single transaction:

```csharp
await using var tx = await db.Database.BeginTransactionAsync();
db.Votes.Add(vote);
await db.Tweets
    .Where(t => t.Id == tweetId)
    .ExecuteUpdateAsync(s => s.SetProperty(t => t.VoteCount, t => t.VoteCount + 1));
await db.SaveChangesAsync();
await tx.CommitAsync();
```

If the `Votes` insert fails (duplicate constraint), `ExecuteUpdateAsync` is never reached. If the update fails after the insert, the transaction rolls back both. `VoteCount` is always consistent with the row count in `Votes` for that tweet.

### 5.2 Folder Deactivation

When a folder is deactivated (`IsActive = false`):
- `FolderTweet` rows are **preserved** — the association is not deleted
- The folder disappears from all public listings
- Tweets in the folder remain accessible via direct URL and search
- A deactivated folder can be reactivated by an Admin
- **Children are not cascade-deactivated.** Each child's `IsActive` is independent. A child of a deactivated parent appears as a root-level folder until the parent is reactivated. This is intentional — avoids accidentally hiding content.

### 5.3 Folder Limits

All limits are enforced in application logic (not DB constraints) and are configurable:

| Limit | Default | Config Key |
|---|---|---|
| Max active folders per Contributor | 50 | `Folders:MaxPerContributor` |
| Max tweets per folder | 1000 | `Folders:MaxTweetsPerFolder` |
| Max nesting depth | 5 levels | `Folders:MaxDepth` |

**Depth calculation:** on `POST /api/folders`, the service walks the ancestor chain (`ParentFolderId → ParentFolderId → ...`) until reaching `NULL`, counting hops. A root folder is depth 1. Returns `409` with `"Maximum folder nesting depth of 5 reached"` if exceeded.

### 5.4 Folder Visibility

New folders are **immediately public**. No approval workflow. Admins can deactivate inappropriate folders via the audit log + API.

### 5.5 XUserProfiles Auto-Creation

An `XUserProfiles` stub is created automatically when a tweet is submitted for an `XUserId` not yet in the table. The stub has:
- `XUserId` — from URL parse (or username as placeholder if numeric ID unavailable until worker scrapes)
- `ScrapedUsername` — from URL path parse
- `CustomName = NULL`, `Description = NULL`, `CreatedByUserId = NULL`

The worker updates `ScrapedUsername` (and canonicalises `XUserId` to the numeric ID) after each successful scrape.

---

## 6. Audit Actions Reference

All write interactions produce an `AuditLog` row. Read interactions are tracked via OTel metrics only.

| Action Key | Trigger |
|---|---|
| `Tweet.Submit` | Stub tweet row inserted and job enqueued |
| `Tweet.Duplicate` | Duplicate tweet detected on submit |
| `Tweet.Scraped` | Worker successfully scraped tweet |
| `Tweet.NotFound` | Worker: tweet deleted/unavailable |
| `Tweet.Private` | Worker: tweet from protected account |
| `Tweet.ScrapeFailed` | Worker exhausted retries |
| `Vote.Cast` | Vote recorded |
| `Vote.Duplicate` | Duplicate vote attempt |
| `Folder.Create` | New folder created |
| `Folder.Update` | Folder name, description, or parent updated |
| `Folder.Deactivate` | Folder deactivated |
| `FolderTweet.Add` | Tweet added to folder |
| `FolderTweet.Remove` | Tweet removed from folder |
| `XUserProfile.AutoCreated` | Stub row created automatically on tweet submission |
| `XUserProfile.Upsert` | Contributor created or updated a profile |
| `User.Create` | Admin adds an authorized user |
| `User.Update` | Admin updates role or active status |
| `User.Deactivate` | Admin deactivates a user |
| `Auth.Login` | Contributor or Admin logs in |
| `Auth.Denied` | Login rejected — user not in `Users` or `IsActive=false` |
| `Auth.Logout` | User explicitly logs out |
| `Auth.CsrfMismatch` | OAuth callback state mismatch |
