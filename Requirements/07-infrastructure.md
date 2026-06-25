# Memory of X — Infrastructure & Deployment

> **Scope:** Docker configuration, Azure Container Apps setup, GitHub Actions CI/CD pipeline (OIDC), blob storage SAS URL pattern, security controls, and implementation order.
> **Read alongside:** `00-overview.md`
> **Note:** Docker is used for production only. Local development runs via F5 — no Docker needed.

---

## 1. Docker (Production Only)

### 1.1 Backend API Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore && dotnet publish src/Api/Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
```

### 1.2 Frontend Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore && dotnet publish src/Frontend/Frontend.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Frontend.dll"]
```

### 1.3 Worker Dockerfile

The Worker is the only image that requires Playwright dependencies. Chromium system libraries must be installed, and the Playwright browser must be downloaded at build time.

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# Chromium system dependencies for Playwright
RUN apt-get update && apt-get install -y \
    libglib2.0-0 libnss3 libatk1.0-0 libatk-bridge2.0-0 \
    libcups2 libdrm2 libxkbcommon0 libxcomposite1 libxdamage1 \
    libxrandr2 libgbm1 libpango-1.0-0 libasound2 --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore && dotnet publish src/Worker/Worker.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN dotnet tool install --global Microsoft.Playwright.CLI && \
    /root/.dotnet/tools/playwright install chromium
ENTRYPOINT ["dotnet", "Worker.dll"]
```

---

## 2. Azure Container Apps Configuration

All three apps share the same Container Apps Environment (`cae-mox-prod`).

### Backend (`ca-mox-api-prod`)
- Ingress: **internal** — not reachable from the internet
- CPU: 0.5 vCPU, Memory: 1Gi
- Min replicas: 0, Max replicas: 3
- Scale rule: HTTP — 50 concurrent requests per replica

### Frontend (`ca-mox-frontend-prod`)
- Ingress: **external** — public HTTPS, TLS provided by ACA
- CPU: 0.5 vCPU, Memory: 1Gi
- Min replicas: 0, Max replicas: 3
- Scale rule: HTTP — 30 concurrent requests per replica
- Env var: `BackendApi__BaseUrl = http://ca-mox-api-prod` (internal ACA DNS)

### Worker (`ca-mox-worker-prod`)
- Ingress: **disabled** — no HTTP port; background process only
- CPU: **1.0 vCPU, Memory: 2Gi** — Chromium requires this; the default 0.5/1Gi causes crashes
- Min replicas: 0, Max replicas: 2
- Scale rule: Azure Storage Queue — scale up when `scrape-jobs` queue depth > 5; scale to zero when empty

Scale-to-zero on all three apps is intentional — near-zero idle cost. Cold start on first request after idle: 5–15s for API/Frontend, up to 30s for Worker (heavier image).

---

## 3. Blob Storage — Screenshot SAS URLs

`ScreenshotBlobName` in the `Tweets` table stores only the blob name (e.g. `1234567890.png`). SAS URLs are generated **on-the-fly at read time** using a **user-delegation SAS** tied to Managed Identity — never stored in the database.

```csharp
// Infrastructure/BlobStorage/BlobStorageService.cs
public async Task<Uri> GetScreenshotSasUriAsync(string blobName)
{
    var userDelegationKey = await _blobServiceClient
        .GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1));

    var sasBuilder = new BlobSasBuilder
    {
        BlobContainerName = "screenshots",
        BlobName = blobName,
        Resource = "b",
        StartsOn  = DateTimeOffset.UtcNow.AddMinutes(-5),
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
    };
    sasBuilder.SetPermissions(BlobSasPermissions.Read);

    return _blobContainerClient
        .GetBlobClient(blobName)
        .GenerateSasUri(sasBuilder, userDelegationKey, _accountName);
}
```

User-delegation SAS uses Entra ID credentials, not storage account keys — so URLs remain valid even when storage keys rotate. URLs expire after 1 hour and are regenerated on each page load.

**Blob container settings:**
- Container name: `screenshots`
- Access level: private (no public blob access)
- Blob naming: `{XTweetId}.png`
- Lifecycle policy: no expiry — permanent archive

---

## 4. Security Controls

| Requirement | Implementation |
|---|---|
| HTTPS | ACA automatic TLS on public ingress; local dev cert |
| No hard-coded secrets | Key Vault + `DefaultAzureCredential`; User Secrets locally |
| SQL injection | EF Core parameterized queries — no raw SQL |
| XSS | Blazor built-in HTML encoding + CSP header (see below) |
| CSRF | Blazor Server SignalR circuit is inherently CSRF-resistant; `AntiForgery` for explicit form POSTs |
| IP capture | `X-Forwarded-For` (ACA sets this); fallback to `RemoteIpAddress` |
| Screenshot blobs | Private container; short-lived user-delegation SAS URLs (1h) |
| Auth token | HttpOnly cookie, `SameSite=Strict`, 8h TTL |
| JWT validation | Backend checks JWT on every protected request + verifies `IsActive` in `Users` table |

### Content Security Policy (Frontend, production only)

```
Content-Security-Policy:
  default-src 'self';
  script-src 'self' 'unsafe-inline';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data: blob: https://stmoxprod.blob.core.windows.net;
  connect-src 'self' wss:;
  frame-ancestors 'none';
  form-action 'self' https://twitter.com;
```

`unsafe-inline` is required by Blazor Server's SignalR and inline event handlers. `img-src` allows the prod blob storage account for tweet screenshots. `connect-src wss:` allows the Blazor SignalR WebSocket. Update `img-src` if the storage account name changes.

---

## 5. GitHub Actions CI/CD

All deployments go through GitHub Actions. No manual image pushes to any environment.

### 5.1 Workflow Triggers

| Trigger | Workflow | Effect |
|---|---|---|
| Push to `main` | `deploy-prod.yml` | Test → build → push to ACR → deploy to prod ACA |
| Push to `dev` | `deploy-dev.yml` | Test → build → push to ACR → deploy to dev ACA |
| PR to `main` or `dev` | `ci.yml` | Test only — no build, no deploy |

### 5.2 Azure Authentication — OIDC Federated Credentials

No long-lived secrets are stored in GitHub. Azure issues a short-lived token at runtime.

**One-time setup per environment:**

```bash
# 1. Create a Service Principal
az ad sp create-for-rbac --name "sp-mox-github-prod" --role Contributor \
  --scopes /subscriptions/{subId}/resourceGroups/rg-mox-prod

# 2. Add a federated credential scoped to the GitHub branch
az ad app federated-credential create \
  --id {appId} \
  --parameters '{
    "name": "github-prod",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:{org}/{repo}:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# 3. Grant RBAC roles to the Service Principal
az role assignment create --assignee {appId} --role AcrPush \
  --scope /subscriptions/{subId}/resourceGroups/rg-mox-prod/providers/Microsoft.ContainerRegistry/registries/acrmoxprod

az role assignment create --assignee {appId} --role Contributor \
  --scope /subscriptions/{subId}/resourceGroups/rg-mox-prod/providers/Microsoft.App/managedEnvironments/cae-mox-prod
```

Repeat for dev, scoped to `refs/heads/dev` and `rg-mox-dev`.

### 5.3 GitHub Secrets

| Secret | Value | Used By |
|---|---|---|
| `AZURE_CLIENT_ID_PROD` | Service Principal Client ID (prod) | `deploy-prod.yml` |
| `AZURE_CLIENT_ID_DEV` | Service Principal Client ID (dev) | `deploy-dev.yml` |
| `AZURE_TENANT_ID` | Azure AD Tenant ID | Both |
| `AZURE_SUBSCRIPTION_ID` | Azure Subscription ID | Both |
| `ACR_LOGIN_SERVER` | `acrmoxprod.azurecr.io` | Both |
| `PROD_RESOURCE_GROUP` | `rg-mox-prod` | `deploy-prod.yml` |
| `DEV_RESOURCE_GROUP` | `rg-mox-dev` | `deploy-dev.yml` |
| `PROD_ACA_ENV` | `cae-mox-prod` | `deploy-prod.yml` |
| `DEV_ACA_ENV` | `cae-mox-dev` | `deploy-dev.yml` |

These are identity references only — no passwords or keys.

### 5.4 Production Workflow (`deploy-prod.yml`)

```yaml
name: Deploy to Production

on:
  push:
    branches: [main]

permissions:
  id-token: write
  contents: read

env:
  IMAGE_TAG: ${{ github.sha }}

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release --logger trx --results-directory ./test-results
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: ./test-results

  build-and-deploy:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Azure login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID_PROD }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Log in to ACR
        run: az acr login --name ${{ secrets.ACR_LOGIN_SERVER }}

      - name: Build and push — API
        run: |
          docker build -f src/Api/Dockerfile \
            -t ${{ secrets.ACR_LOGIN_SERVER }}/api:${{ env.IMAGE_TAG }} .
          docker push ${{ secrets.ACR_LOGIN_SERVER }}/api:${{ env.IMAGE_TAG }}

      - name: Build and push — Frontend
        run: |
          docker build -f src/Frontend/Dockerfile \
            -t ${{ secrets.ACR_LOGIN_SERVER }}/frontend:${{ env.IMAGE_TAG }} .
          docker push ${{ secrets.ACR_LOGIN_SERVER }}/frontend:${{ env.IMAGE_TAG }}

      - name: Build and push — Worker
        run: |
          docker build -f src/Worker/Dockerfile \
            -t ${{ secrets.ACR_LOGIN_SERVER }}/worker:${{ env.IMAGE_TAG }} .
          docker push ${{ secrets.ACR_LOGIN_SERVER }}/worker:${{ env.IMAGE_TAG }}

      - name: Deploy API
        uses: azure/container-apps-deploy-action@v1
        with:
          resourceGroup: ${{ secrets.PROD_RESOURCE_GROUP }}
          containerAppName: ca-mox-api-prod
          imageToDeploy: ${{ secrets.ACR_LOGIN_SERVER }}/api:${{ env.IMAGE_TAG }}

      - name: Deploy Frontend
        uses: azure/container-apps-deploy-action@v1
        with:
          resourceGroup: ${{ secrets.PROD_RESOURCE_GROUP }}
          containerAppName: ca-mox-frontend-prod
          imageToDeploy: ${{ secrets.ACR_LOGIN_SERVER }}/frontend:${{ env.IMAGE_TAG }}

      - name: Deploy Worker
        uses: azure/container-apps-deploy-action@v1
        with:
          resourceGroup: ${{ secrets.PROD_RESOURCE_GROUP }}
          containerAppName: ca-mox-worker-prod
          imageToDeploy: ${{ secrets.ACR_LOGIN_SERVER }}/worker:${{ env.IMAGE_TAG }}
```

Each `container-apps-deploy-action` triggers a **rolling revision update** — ACA keeps the previous revision live until the new one passes its `/health/live` and `/health/ready` probes. If health checks fail, the new revision is deactivated automatically.

### 5.5 Dev Workflow (`deploy-dev.yml`)

Identical to prod with:
- Trigger: push to `dev`
- Secrets: `AZURE_CLIENT_ID_DEV`, `DEV_RESOURCE_GROUP`, `DEV_ACA_ENV`
- Container app names: `ca-mox-api-dev`, `ca-mox-frontend-dev`, `ca-mox-worker-dev`

### 5.6 PR Workflow (`ci.yml`)

```yaml
name: CI

on:
  pull_request:
    branches: [main, dev]

permissions:
  contents: read

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release
```

Enable branch protection on `main` and `dev` — require this workflow to pass before any PR can merge.

### 5.7 Workflow File Locations

```
.github/
  workflows/
    ci.yml            # PR check — test only
    deploy-dev.yml    # Push to dev → deploy to dev ACA
    deploy-prod.yml   # Push to main → deploy to prod ACA
```

---

## 6. Implementation Order

Build in this order to minimize blocked dependencies. Each step should be committed and tested before moving to the next.

1. **Database** — EF Core entities (`FetchStatus` enum, `Tweets` nullable columns, `Folders.ParentFolderId` self-reference, `XUserProfiles` table), `AppDbContext`, all migrations, `DatabaseSeeder`
2. **Infrastructure: Queue** — `ScrapeQueueService`, `ScrapeJobMessage` DTO
3. **Backend: Foundation** — project setup, `DefaultAzureCredential` config chain, `CorrelationIdMiddleware`, `HttpLoggingMiddleware` (request/response logging + `http.server.request` histogram), `LoadSheddingMiddleware`, OTel, health checks (`/health/live`, `/health/ready`), DI wiring, CORS (dev only)
4. **Backend: Auth endpoint** — JWT generation (HS256), user lookup, `IsActive` check, `GET /api/auth/verify`
5. **Backend: Tweet submission** — URL parse (`XTweetId` + `AuthorXUsername`), dedup check, stub `Tweet` insert, `XUserProfiles` stub auto-create (upsert), `FolderTweet` insert, `AuditLog`, queue enqueue; return `202 Accepted`
6. **Backend: Status polling** — `GET /api/tweets/{id}/status`
7. **Backend: Search** — full-text + username + userId, `XUserProfile` join, AND logic, pagination, sorting
8. **Backend: Folders** — CRUD with `parentFolderId`, depth validation (max 5), ancestor traversal, root list, folder-with-children, children endpoint, deactivation, `FolderTweet` management
9. **Backend: XUserProfiles** — `GET /api/xusers/{xUserId}` (public), `PUT /api/xusers/{xUserId}` (Contributor — upsert, `400` if no fields provided)
10. **Backend: Votes** — authenticated + anonymous dedup, `VoteCount` transaction, `409` on duplicate
11. **Backend: Admin** — user CRUD, deactivation
12. **Backend: Rate limiting** — in-memory (local) / Redis (prod) abstraction, all policies
13. **Worker: Foundation** — Worker Service project, `DefaultAzureCredential` config chain, OTel, polling loop, idle backoff
14. **Worker: Playwright scraping** — headless Chromium with `--no-sandbox` and `--disable-dev-shm-usage`, public tweet navigation, DOM extraction, element screenshot
15. **Worker: Blob upload + DB update** — upload PNG, `XUserProfiles` upsert, SQL transaction, `AuditLog`, queue delete-message
16. **Worker: Error handling** — tombstone detection, protected account detection, `DequeueCount >= 3` → `ScrapeFailed`, blob failure non-blocking
17. **Frontend: Foundation** — Blazor Server project, `DefaultAzureCredential` config chain, typed `ApiClient` with `LoggingDelegatingHandler`, OTel, middleware pipeline
18. **Frontend: X OAuth 2.0 PKCE** — login redirect, CSRF state, callback handler, session cookie, `401` redirect
19. **Frontend: Logout** — cookie clear, audit log, redirect
20. **Frontend: Landing page** — search (with `SubjectProfileCard` on userId search), root folder grid, submit input with `ScrapeStatusPoller`, mobile-responsive layout
21. **Frontend: Folder pages** — root folder list, `FolderDetail` with `FolderBreadcrumb`, child folder cards, tweet list, mobile-responsive
22. **Frontend: XUserProfile page** — `/xusers/{xUserId}`, profile display, tweet list, contributor edit form
23. **Frontend: Contributor features** — `FolderSelector` (tree-aware, bottom-sheet on mobile), create subfolder from folder detail, depth warning at level 4
24. **Frontend: Admin pages** — user management, audit log viewer with filters
25. **Frontend: CSP header** — `Content-Security-Policy` middleware, production only
26. **VS Launch Configuration** — three startup projects (Api, Frontend, Worker); create dev queue `scrape-jobs`; confirm F5 starts all three cleanly
27. **Docker** — finalize all three Dockerfiles; verify Playwright Chromium installs correctly in Worker image; test image builds locally
28. **CI/CD** — create `.github/workflows/ci.yml`, `deploy-dev.yml`, `deploy-prod.yml`; create Service Principals for dev and prod; configure OIDC federated credentials; grant `AcrPush` and `Contributor` roles; add all GitHub secrets; enable branch protection on `main` and `dev`
29. **Azure Provisioning** — provision dev and prod resource groups; create storage queues; configure ACA Worker scale rule on queue depth; assign Managed Identity RBAC roles
30. **End-to-end testing** — API integration tests (submit→poll, folder tree, XUserProfile upsert, vote dedup); Worker integration tests with real queue; Playwright browser tests for polling UX, folder breadcrumb, mobile layouts
