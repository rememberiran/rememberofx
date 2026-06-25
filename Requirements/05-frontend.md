# Memory of X — Frontend Service

> **Scope:** Blazor Server project structure, responsive design requirements, pages and routes, all UI behaviours, X OAuth 2.0 PKCE flow, logout, session expiry, and frontend rate limiting.
> **Read alongside:** `00-overview.md`, `02-backend-api.md`, `03-middleware.md`, `06-observability.md`
> **Technology:** ASP.NET Core Blazor Server, C# 12

---

## 1. Technology

- **Framework:** ASP.NET Core Blazor **Server** (server-side rendering, C# 12)
- Runs as a plain .NET process locally (F5); runs as a Docker container in production on ACA
- Communicates with Backend API via typed `HttpClient` (`IHttpClientFactory`)
- ACA ingress: **external** (public HTTPS) — the only publicly reachable service
- **The UI must be fully responsive and usable on mobile devices.** All pages must work correctly at viewport widths from 375px upward without horizontal scrolling.

---

## 2. Responsive Design Requirements

Most users are expected to visit on a phone — sharing a tweet link from the X mobile app and landing on the site.

**CSS approach:** mobile-first. Base styles target small screens; media queries add layout changes for wider viewports.

**Breakpoints:**
- `< 640px` — single-column layout; full-width cards; stacked controls
- `640px–1024px` — two-column folder/tweet grid; side-by-side search controls
- `> 1024px` — three-column folder grid; wider content area

**Per-component mobile behaviour:**

| Component | Mobile behaviour |
|---|---|
| `NavMenu` | Hamburger menu at `< 640px`; full nav bar on desktop |
| `SearchBar` | Full-width input; mode toggle stacks below on mobile |
| `TweetCard` | Full-width; screenshot scales to fit; text truncated at 3 lines with expand toggle |
| `FolderCard` | Single column on mobile; 2-col tablet; 3-col desktop |
| `FolderBreadcrumb` | Truncates middle ancestors with `…`; always shows root and current node |
| `FolderSelector` | Bottom-sheet drawer on mobile; dropdown on desktop |
| `VoteButton` | Min 44×44px tap target |
| `ScrapeStatusPoller` | Inline status banner — no modal |
| `PaginationControl` | Prev/Next only on mobile; full page numbers on desktop |
| Submit URL input | Full-width with large padding; submit button stacked below on mobile |

**Screenshot images:** `max-width: 100%; height: auto` — fills card width on mobile. No lightbox in v1.0.

**Touch targets:** minimum 44×44px on all interactive elements per WCAG 2.1.

---

## 3. Project Structure

```
src/Frontend/
  Pages/
    Index.razor               # Landing — search, root folder grid, submit input
    TweetDetail.razor         # /tweets/{id}
    Folders.razor             # /folders — root folder browse
    FolderDetail.razor        # /folders/{id} — breadcrumb, child folders, tweets
    XUserProfile.razor        # /xusers/{xUserId} — profile + tweet list + edit form
    Auth/
      Callback.razor          # /auth/callback — X OAuth callback handler
      Logout.razor            # /auth/logout — clears cookie, redirects to landing
    Admin/
      Users.razor             # /admin/users
      AuditLog.razor          # /admin/audit

  Components/
    TweetCard.razor
    FolderCard.razor
    FolderBreadcrumb.razor    # Ancestor chain as linked breadcrumb
    SubjectProfileCard.razor  # XUserProfile custom name and description
    SearchBar.razor
    VoteButton.razor
    FolderSelector.razor      # Multi-select folders; tree structure; bottom-sheet on mobile
    PaginationControl.razor
    EmptyState.razor          # Shared empty-state display
    SessionExpiredBanner.razor
    ScrapeStatusPoller.razor  # Polls /api/tweets/{id}/status; progress → result card

  Services/
    ApiClient.cs              # Typed HttpClient — all Backend API calls
    AuthStateService.cs
    LoggingDelegatingHandler.cs  # Logs outbound requests/responses with CorrelationId in scope

  Layout/
    MainLayout.razor
    NavMenu.razor             # Responsive — hamburger on mobile

  wwwroot/
    css/
    js/

  Program.cs
  appsettings.json
  appsettings.Development.json
  appsettings.Production.json
```

---

## 4. Configuration

### `appsettings.Development.json` (committed, non-sensitive)

```json
{
  "KeyVault": { "Uri": "https://kv-mox-dev.vault.azure.net/" },
  "BackendApi": { "BaseUrl": "https://localhost:5001" },
  "AzurePrimaryRegion": "eastus",
  "XOAuth": { "RedirectUri": "https://localhost:5000/auth/callback" },
  "Otel": { "UseConsoleExporter": true }
}
```

### `appsettings.Production.json` (committed, no secrets)

```json
{
  "KeyVault": { "Uri": "https://kv-mox-prod.vault.azure.net/" },
  "AzurePrimaryRegion": "eastus"
}
```

### User Secrets (local only, never committed)

```json
{
  "XOAuth": { "ClientId": "...", "ClientSecret": "..." },
  "Jwt": { "Secret": "local-dev-jwt-secret-min-32-chars" },
  "SessionCookie": { "EncryptionKey": "..." }
}
```

### Production Environment Variables (via ACA Key Vault references)

| Variable | Key Vault Secret |
|---|---|
| `BackendApi__BaseUrl` | (literal — internal ACA DNS, e.g. `http://ca-mox-api-prod`) |
| `XOAuth__ClientId` | `x-oauth-client-id` |
| `XOAuth__ClientSecret` | `x-oauth-client-secret` |
| `XOAuth__RedirectUri` | (literal — `https://memoryofx.com/auth/callback`) |
| `Jwt__Secret` | `jwt-secret` |
| `SessionCookie__EncryptionKey` | `session-cookie-key` |
| `ApplicationInsights__ConnectionString` | `app-insights-connection-string` |
| `AZURE_PRIMARY_REGION` | (literal env var) |

---

## 5. Pages & Routes

| Route | Page | Access |
|---|---|---|
| `/` | Landing — search bar, root folder grid, submit tweet input | Anonymous |
| `/tweets/{id}` | Single tweet detail with screenshot | Anonymous |
| `/folders` | Browse root-level folders | Anonymous |
| `/folders/{id}` | Folder detail — breadcrumb, child folders, tweets | Anonymous |
| `/xusers/{xUserId}` | X user profile — name, description, archived tweets | Anonymous |
| `/auth/callback` | X OAuth callback | Anonymous (during login) |
| `/auth/logout` | Clears session cookie, redirects to `/` | Authenticated |
| `/admin/users` | Manage authorized users | Admin only |
| `/admin/audit` | Audit log viewer with filters | Admin only |

---

## 6. Key UI Behaviours

### Submit Tweet (all users)

- URL input visible on landing page — no login required.
- On `409 Conflict`: show existing tweet card or pending indicator — **"Already submitted"** with current status.
- On `202 Accepted`: show inline progress banner — **"Archiving tweet… checking every few seconds."**
  - `ScrapeStatusPoller` begins polling `GET /api/tweets/{id}/status` every **3 seconds**.
  - `fetchStatus='Ok'` → replace banner with completed `TweetCard`.
  - `fetchStatus='NotFound'` → **"Tweet Deleted"** badge with original URL and archive timestamp.
  - `fetchStatus='Private'` → **"Protected Account"** — URL is archived but content unavailable.
  - `fetchStatus='ScrapeFailed'` → **"Could not retrieve this tweet. The URL has been saved and will be retried."**
  - After **2 minutes** with no terminal status → **"This tweet is taking longer than expected. Please check back later."** — polling stops.
- If logged-in Contributor: show `FolderSelector` immediately after `202 Accepted` (folder assignment is saved at submission time, not after scraping).

### Vote Button

- Displayed on every `TweetCard`.
- Shows current `VoteCount`.
- On `409 Conflict` (duplicate): button changes to **"Voted"** state — no error shown.
- Tooltip: **"Vote count reflects community interest. Anonymous votes are limited to one per IP address."**
- Logged-in users are deduped by identity; if they previously voted anonymously, **"Voted"** state shows immediately on login.

### Search

- Mode toggle: By Keyword / By Username / By User ID.
- Multiple filters simultaneously (AND logic).
- Sort toggle: Most Voted (default) / Most Recent.
- Paginated `TweetCard` list.
- When searching by User ID and an `XUserProfile` exists: show `SubjectProfileCard` above results.
- Each `TweetCard` shows `customName` from `XUserProfile` if available; scraped username shown as subtitle.
- Zero results: `EmptyState` — **"No archived tweets found. Try a different search."**
- Tracked via OTel metric `tweet.search`.

### Folder Browse

- Landing page: root folder cards grid — name, description, direct tweet count, child folder count badge.
- `/folders/{id}`:
  - Breadcrumb trail (e.g. **Subjects > Iran Officials > Ministers**) — each crumb is a link.
  - Child folder cards above the tweet list.
  - Tweets in this folder — sort toggle (Most Voted / Most Recent).
  - Only active folders and `FetchStatus='Ok'` tweets shown.
- Zero content: `EmptyState` — **"This folder has no archived tweets yet."**
- Tracked via OTel metric `folder.viewed`.

### X User Profile Page (`/xusers/{xUserId}`)

- `SubjectProfileCard` at top with `CustomName` and `Description`.
- If profile has no `CustomName`: show scraped `AuthorXUsername` as heading + **"No profile has been added for this user yet."**
- Paginated tweet list — Most Voted / Most Recent.
- Contributor: **"Edit Profile"** button opens inline form to set/update `CustomName` and `Description`.

### Contributor Folder Management

- **"New Subfolder"** button on folder detail pages — pre-fills `parentFolderId`.
- Breadcrumb in create-folder form shows placement.
- At depth 4: **"Note: this folder can have one more level of subfolders."**
- At depth 5: **"New Subfolder"** button is hidden.

### Session Expiry

- On any API `401`: clear cookie, redirect to `/?sessionExpired=true`.
- `SessionExpiredBanner` on landing page reads query param and shows: **"Your session has expired. Please log in again."**

---

## 7. Authentication — X.com OAuth 2.0 with PKCE

1. User clicks Login → redirect to `https://twitter.com/i/oauth2/authorize` with `client_id`, `redirect_uri`, `scope=tweet.read users.read`, `code_challenge` (PKCE S256), `state` (CSRF token stored in session).
2. X redirects to `/auth/callback?code=xxx&state=yyy`.
3. Verify `state` matches session CSRF token. On mismatch: return `400`, log `Auth.CsrfMismatch`.
4. Exchange code: `POST https://api.twitter.com/2/oauth2/token`.
5. Call `GET https://api.twitter.com/2/users/me` to get `id` and `username`.
6. Call Backend `GET /api/auth/verify?xUserId={id}` — verifies user is in `Users` table with `IsActive=true`, returns signed JWT.
7. Store JWT in **secure HttpOnly cookie** (`SameSite=Strict`, 8h expiry).
8. Write audit log `Auth.Login`.
9. If backend returns `403`: show **"Access denied — your X account is not registered."** Write audit log `Auth.Denied`.

**Logout:**
- Navigate to `/auth/logout`.
- Delete session cookie (`Response.Cookies.Delete("auth")`).
- Call `AuthStateService.Clear()`.
- Write audit log `Auth.Logout`.
- Redirect to `/`.
- The X access token is not revoked at X (limited scopes, not necessary).

---

## 8. Frontend Rate Limiting

Same in-memory (local) / Redis (production) abstraction as the backend. See `03-middleware.md` for implementation pattern.

| Policy | Scope | Limit |
|---|---|---|
| `page-load` | Per IP | 120 requests per minute |
| `auth-callback` | Per IP | 10 requests per 5 minutes |
| `global` | Per IP | 200 requests per minute |
