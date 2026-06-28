# Backend API Gaps for Frontend

Each task below addresses a specific data gap between what the frontend needs and what the backend currently provides. Tasks are independent and can be implemented in any order.

---

## Task 1: Add tweet count to FolderSummaryDto

**Problem:** `FolderSummaryDto` only has `ChildCount` (subfolder count). The frontend needs to display how many tweets are filed under each folder on the Home, Browse, and Folder Detail pages.

**What to change:**

1. **`src/Application/Models/FolderDto.cs`** — Add `int TweetCount` parameter to the `FolderSummaryDto` record (after `ChildCount`).

2. **`src/Application/Models/FolderSummary.cs`** — Add `int TweetCount` to the `FolderSummary` record.

3. **`src/Application/Services/FolderService.cs`** — In `ListRootFoldersAsync()` (line 36-45), update the `.Select()` projection to also count tweets:
   ```csharp
   .Select(f => new
   {
       Record = f,
       ChildCount = f.Children.Count(c => c.IsActive),
       TweetCount = f.FolderTweets.Count,
   })
   ```
   Pass `TweetCount` into the `FolderSummary` constructor.

   Apply the same change in `GetChildrenAsync()` (line 71-86).

4. **`src/Api/Mappers/FolderDtoMapper.cs`** — Update both `ToSummaryDto` overloads to pass `TweetCount` through:
   - `ToSummaryDto(FolderSummary summary)` — pass `summary.TweetCount`
   - `ToSummaryDto(Folder folder, int activeChildCount)` — this is used for children inside `GetById`. It needs a tweet count parameter too, or default to 0 and fix the caller.

5. **`src/Api/Controllers/FoldersController.cs`** — In `GetById()` (line 59-60), when building the children list, also compute each child's tweet count. The simplest approach: query `FolderTweets` counts for the child IDs, or extend the `GetByIdAsync` service method to include children with counts.

**Verify:** `GET /api/folders` and `GET /api/folders/{id}` (children array) should now include `tweetCount` in each folder summary.

---

## Task 2: Add folder icon field

**Problem:** Folders have no icon field. The frontend currently hardcodes a lookup table mapping folder names to Bootstrap Icon class names (e.g., "Politics" → "bi-bank"). This should be a persisted field so users can set it when creating/editing folders.

**What to change:**

1. **`src/Storage/FolderRecord.cs`** — Add property: `public string? Icon { get; set; }`

2. **`src/Domain/Entities/Folder.cs`** — Add property: `public string? Icon { get; set; }`

3. **`src/Domain/Mappers/FolderMapper.cs`** — Map `Icon` in both `ToDomain()` and `ToRecord()`.

4. **`src/Infrastructure/Data/AppDbContext.cs`** — In `OnModelCreating`, add column config for `Folder.Icon`: `entity.Property(e => e.Icon).HasMaxLength(50)`.

5. **EF Migration:** Run `dotnet ef migrations add AddFolderIcon --project src/Infrastructure --startup-project src/Api`.

6. **`src/Application/Models/FolderDto.cs`** — Add `string? Icon` to both `FolderSummaryDto` and `FolderDto`.

7. **`src/Api/Mappers/FolderDtoMapper.cs`** — Pass `folder.Icon` through in `ToDto()` and `ToSummaryDto()`.

8. **`src/Api/Models/Requests/`** — Add `string? Icon` (max length 50) to `CreateFolderRequest` and `UpdateFolderRequest`.

9. **`src/Api/Controllers/FoldersController.cs`** — Pass `request.Icon` to service `CreateAsync` / `UpdateAsync`.

10. **`src/Application/Interfaces/IFolderService.cs`** and **`src/Application/Services/FolderService.cs`** — Add `string? icon` parameter to `CreateAsync` and `UpdateAsync`.

**Verify:** Creating a folder with `{ "name": "Politics", "icon": "bi-bank" }` persists the icon. `GET /api/folders` returns it.

---

## Task 3: Add folder assignments to TweetDto

**Problem:** `TweetDto` does not include which folders a tweet belongs to. The frontend needs this to render folder chips on every tweet card and on the tweet detail page.

**What to change:**

1. **`src/Application/Models/TweetDto.cs`** — Add a new field to `TweetDto`:
   ```csharp
   IReadOnlyList<TweetFolderDto> Folders = null
   ```
   Add a new record:
   ```csharp
   public record TweetFolderDto(Guid FolderId, string FolderName);
   ```

2. **`src/Api/Mappers/TweetDtoMapper.cs`** — In `ToDto(Tweet tweet, ...)`, populate the `Folders` field. The `Tweet` domain entity has a `FolderTweets` navigation collection. Map it:
   ```csharp
   var folders = tweet.FolderTweets
       .Select(ft => new TweetFolderDto(ft.FolderId, ft.Folder.Name))
       .ToList();
   ```
   Pass `folders` to the TweetDto constructor.

3. **Ensure eager loading:** Wherever tweets are queried for API responses, ensure `FolderTweets` (and its `Folder` navigation) is included. Check these locations:
   - `src/Application/Services/TweetQueryService.cs` — `GetByIdAsync` query: add `.Include(t => t.FolderTweets).ThenInclude(ft => ft.Folder)` (only include `Id` and `Name` from Folder via projection if performance is a concern).
   - `src/Application/Services/FolderService.cs` — `GetTweetsAsync` query (line 96-111): the join already touches `FolderTweets`, but only for filtering. Add `.Include(t => t.FolderTweets).ThenInclude(ft => ft.Folder)` to the tweet query.
   - `src/Application/Services/SearchService.cs` — same pattern for search results.

   Note: The `TweetWithAuthor` record wraps a `Tweet` domain entity. The `Tweet` entity already declares `ICollection<FolderTweet> FolderTweets`. The `FolderTweet` entity has `Folder Folder` navigation. So the data path exists — it just needs the Include.

4. **Storage records:** `FolderTweetRecord` already has `FolderRecord Folder` navigation. `TweetRecord` already has `ICollection<FolderTweetRecord> FolderTweets`. No schema changes needed.

**Verify:** `GET /api/tweets/{id}` and search results should include `"folders": [{ "folderId": "...", "folderName": "US Elections" }]`.

---

## Task 4: Add submitter/capture info to TweetDto

**Problem:** `TweetDto` has no information about who submitted (archived) the tweet or when. The domain entity has `SubmittedByUserId` and `CreatedAt` (archive timestamp), and a `SubmittedByUser` navigation property, but none of this reaches the API response.

**What to change:**

1. **`src/Application/Models/TweetDto.cs`** — Add fields to `TweetDto`:
   ```csharp
   string? SubmittedByUsername = null,
   DateTime? SubmittedAt = null
   ```
   `SubmittedAt` is the same as `CreatedAt` (already in the DTO), but giving it a semantic alias in the frontend model mapping is cleaner than reinterpreting `CreatedAt`. Alternatively, just add `SubmittedByUsername` and let the frontend use the existing `CreatedAt` as the capture timestamp.

   Simplest change: add only `string? SubmittedByUsername = null` to TweetDto.

2. **`src/Api/Mappers/TweetDtoMapper.cs`** — In `ToDto()`, map `tweet.SubmittedByUser?.XUsername` to the new field:
   ```csharp
   submittedByUsername: tweet.SubmittedByUser?.XUsername
   ```

3. **Ensure eager loading:** Wherever tweets are queried, include `SubmittedByUser`. Check:
   - `src/Application/Services/TweetQueryService.cs` — `GetByIdAsync`: add `.Include(t => t.SubmittedByUser)`.
   - `src/Application/Services/FolderService.cs` — `GetTweetsAsync`: add `.Include(t => t.SubmittedByUser)` to the tweet query.
   - `src/Application/Services/SearchService.cs` — same.

   Note: `TweetRecord` already has `UserRecord? SubmittedByUser` navigation. `Tweet` entity has `User? SubmittedByUser`. The mapper (`TweetMapper.ToDomain`) must also map this navigation — check `src/Domain/Mappers/TweetMapper.cs` to confirm it copies `SubmittedByUser`.

4. **Domain mapper check:** If `TweetMapper.ToDomain()` doesn't map the `SubmittedByUser` navigation to the domain entity, add it. The pattern would be:
   ```csharp
   SubmittedByUser = record.SubmittedByUser != null ? UserMapper.ToDomain(record.SubmittedByUser) : null
   ```

**Verify:** `GET /api/tweets/{id}` should include `"submittedByUsername": "curator1"`.

---

## Task 5: Add vote status check for current user

**Problem:** The frontend cannot show whether the current user has already voted on a tweet. The `Vote` table stores `VoterUserId`, but there's no API to check vote status.

**What to change — Option A (preferred): Add `IsVotedByMe` to TweetDto:**

1. **`src/Application/Models/TweetDto.cs`** — Add `bool IsVotedByMe = false` to `TweetDto`.

2. **`src/Api/Mappers/TweetDtoMapper.cs`** — Accept a `HashSet<Guid>? votedTweetIds` parameter (set of tweet IDs the current user has voted on). In `ToDto()`, set `isVotedByMe: votedTweetIds?.Contains(tweet.Id) ?? false`. Update `ToDtoList()` similarly.

3. **Controller layer:** In each controller that returns tweets, look up the current user's votes:
   ```csharp
   var userId = _identityContext.Value?.InternalUserId;
   HashSet<Guid>? votedIds = null;
   if (userId.HasValue)
   {
       var tweetIds = tweets.Select(t => t.Id).ToList();
       votedIds = (await _db.Votes
           .Where(v => v.VoterUserId == userId && tweetIds.Contains(v.TweetId))
           .Select(v => v.TweetId)
           .ToListAsync(ct))
           .ToHashSet();
   }
   ```
   Pass `votedIds` to the mapper. Apply in:
   - `FoldersController.GetTweets()`
   - `SearchController.Search()`
   - `TweetsController.GetById()` (single tweet — just check one vote)

   For this to work, controllers that need vote lookup will need `IAppDbContext` injected (or create a small `IVoteQueryService` with a `GetVotedTweetIdsAsync(Guid userId, IEnumerable<Guid> tweetIds)` method).

**What to change — Option B (separate endpoint):**

1. **`src/Api/Controllers/VotesController.cs`** — Add a new endpoint:
   ```csharp
   [HttpGet("mine")]
   public async Task<IActionResult> GetMyVotes([FromQuery] Guid[] tweetIds, CancellationToken ct)
   ```
   Returns the subset of `tweetIds` that the current user has voted on. The frontend calls this after loading tweets and patches the local state.

2. **`src/Application/Interfaces/IVoteService.cs`** — Add:
   ```csharp
   Task<Result<HashSet<Guid>>> GetVotedTweetIdsAsync(Guid userId, IReadOnlyList<Guid> tweetIds, CancellationToken ct);
   ```

3. **`src/Application/Services/VoteService.cs`** — Implement by querying `Votes` table filtered by `VoterUserId` and the provided tweet IDs.

**Recommendation:** Option A is better UX (no extra round trip), but Option B is simpler to implement and doesn't touch TweetDto. Pick Option A if you want a clean single-request page load.

**Verify:** Tweets returned from the API should include `"isVotedByMe": true/false` when the user is authenticated.

---

## Task 6: Add author aggregate stats to profile

**Problem:** The Profile page (`/u/{xUserId}`) needs to show total archived tweet count, total votes received across all tweets, and the date the author's first tweet was archived. The `XUserProfileDto` has none of these.

**What to change:**

1. **`src/Application/Models/XUserProfileDto.cs`** — Add fields:
   ```csharp
   int ArchivedTweetCount = 0,
   int TotalVotesReceived = 0,
   DateTime? FirstArchivedAt = null
   ```

2. **`src/Api/Mappers/XUserProfileDtoMapper.cs`** — Update `ToDto()` to accept optional stats parameters:
   ```csharp
   public static XUserProfileDto ToDto(
       XUserProfile profile,
       int archivedTweetCount = 0,
       int totalVotesReceived = 0,
       DateTime? firstArchivedAt = null)
   ```

3. **`src/Api/Controllers/XUserProfilesController.cs`** — In `GetByXUserId()`, after fetching the profile, query aggregate stats from tweets:
   ```csharp
   var stats = await _db.Tweets
       .Where(t => t.AuthorXUserId == xUserId && t.FetchStatus == "Ok")
       .GroupBy(_ => 1)
       .Select(g => new
       {
           Count = g.Count(),
           TotalVotes = g.Sum(t => t.VoteCount),
           FirstArchived = g.Min(t => t.CreatedAt),
       })
       .FirstOrDefaultAsync(ct);
   ```
   Pass stats to the mapper. The controller will need `IAppDbContext` injected (or create a helper method on the profile service).

4. **Alternative:** Create a dedicated `IXUserProfileService.GetWithStatsAsync()` method that returns the profile + stats in one call, keeping the controller thin.

**Verify:** `GET /api/xusers/{xUserId}` should include `"archivedTweetCount": 24, "totalVotesReceived": 1203, "firstArchivedAt": "2026-03-15T..."`.

---

## Task 7: Add search/filter by submitter user ID

**Problem:** The My Archive page needs to show tweets submitted by the current user. The search API only filters by author (`username`/`userId` = X author), not by who submitted (archived) the tweet.

**What to change:**

1. **`src/Application/Models/SearchTweetsQuery.cs`** — Add a new parameter:
   ```csharp
   Guid? SubmittedByUserId = null
   ```

2. **`src/Application/Services/SearchService.cs`** — In the query builder, add a filter:
   ```csharp
   if (query.SubmittedByUserId.HasValue)
   {
       tweetsQuery = tweetsQuery.Where(t => t.SubmittedByUserId == query.SubmittedByUserId);
   }
   ```

3. **`src/Api/Controllers/SearchController.cs`** — Add `[FromQuery] Guid? submittedBy` parameter to the `Search` action. Pass it to `SearchTweetsQuery`.

**Verify:** `GET /api/search?submittedBy={userId}` returns only tweets submitted by that user.

---

## Task 8: Add endpoint to list folders by creator

**Problem:** The My Archive page needs to show folders created by the current user. There's no API to filter folders by `CreatedByUserId`.

**What to change:**

1. **`src/Application/Interfaces/IFolderService.cs`** — Add:
   ```csharp
   Task<Result<List<FolderSummary>>> ListByCreatorAsync(Guid userId, CancellationToken ct);
   ```

2. **`src/Application/Services/FolderService.cs`** — Implement `ListByCreatorAsync`:
   ```csharp
   public async Task<Result<List<FolderSummary>>> ListByCreatorAsync(Guid userId, CancellationToken ct)
   {
       var folders = await _db.Folders.AsNoTracking()
           .Include(f => f.CreatedByUser)
           .Where(f => f.CreatedByUserId == userId && f.IsActive)
           .Select(f => new
           {
               Record = f,
               ChildCount = f.Children.Count(c => c.IsActive),
               TweetCount = f.FolderTweets.Count,
           })
           .ToListAsync(ct);

       return Result.Success(folders
           .Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, f.TweetCount))
           .ToList());
   }
   ```
   Note: If Task 1 has been completed, `FolderSummary` already has `TweetCount`. If not, just use `ActiveChildCount` for now.

3. **`src/Api/Controllers/FoldersController.cs`** — Add a new endpoint:
   ```csharp
   [HttpGet("mine")]
   [Authorize(Roles = "Contributor,Admin")]
   [ProducesResponseType(typeof(List<FolderSummaryDto>), StatusCodes.Status200OK)]
   public async Task<IActionResult> ListMine(CancellationToken ct)
   {
       var userId = _identityContext.Value?.InternalUserId;
       if (userId is null)
       {
           return Unauthorized();
       }

       var result = await _folderService.ListByCreatorAsync(userId.Value, ct);
       if (!result.IsSuccess)
       {
           return result.ToActionResult();
       }

       var dtos = result.Value!.Select(FolderDtoMapper.ToSummaryDto).ToList();
       return Ok(dtos);
   }
   ```

**Important:** Place this `[HttpGet("mine")]` action **before** the `[HttpGet("{id:guid}")]` action in the controller to avoid route conflicts ("mine" being interpreted as a GUID).

**Verify:** `GET /api/folders/mine` (authenticated) returns only folders created by the calling user.

---

## Frontend Update Tasks (after backend gaps are filled)

Once the backend tasks above are completed, the following frontend files need updating:

| Backend Task | Frontend Files to Update |
|---|---|
| Task 1 (folder tweet count) | `ModelMappers.cs` — use `dto.TweetCount` instead of hardcoded `0` |
| Task 2 (folder icon) | `ModelMappers.cs` — use `dto.Icon ?? GetFolderIcon(dto.Name)` as fallback |
| Task 3 (tweet folders) | `ModelMappers.cs` — map `dto.Folders` to `FolderChipViewModel` list |
| Task 4 (submitter info) | `ModelMappers.cs` — map `dto.SubmittedByUsername` + `dto.CreatedAt` to `CaptureInfoViewModel` |
| Task 5 (vote status) | `ViewModels.cs` — add `IsVoted` bool; `ApiClient.cs` — handle new field |
| Task 6 (profile stats) | `ProfileOrchestrator.cs` — use stats from `XUserProfileDto` directly |
| Task 7 (search by submitter) | `ApiClient.cs` — add `submittedBy` param; `MyArchiveOrchestrator.cs` — use it |
| Task 8 (folders by creator) | `ApiClient.cs` — add `GetMyFoldersAsync()`; `MyArchiveOrchestrator.cs` — call it |
