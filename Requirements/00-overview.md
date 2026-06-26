# Memory of X — Overview & Environments

> **Scope:** Project purpose, user roles, environment setup, and Azure resource list.
> **Read this first.** Every other document assumes this context.
> **Related docs:** All documents depend on this one.

---

## 1. Project Purpose

**Memory of X** is a web application that captures, archives, and surfaces tweets from X (formerly Twitter) to serve as a public record of propaganda and harmful content spread against the Iranian people. Anonymous users can submit tweet URLs and search archived tweets. Authorized contributors can curate content into folders and vote on archived tweets. The UI must be fully usable on mobile devices — most users are expected to visit on a phone, especially when sharing a tweet link directly from the X mobile app.

---

## 2. User Roles

| Role | How They Qualify | Capabilities |
|---|---|---|
| Anonymous | Any visitor | Submit tweet URLs, search tweets, browse folders, vote |
| Contributor | Admin adds their X user ID to the `Users` table | All anonymous actions + login via X SSO, create folders, assign tweets to folders, create and update custom profiles (name and description) for archived X users |
| Admin | Pre-configured in database seed | All Contributor actions + manage authorized user list, view audit log |

---

## 3. Services

The application consists of three .NET services:

| Service | Technology | Purpose |
|---|---|---|
| **Backend API** | ASP.NET Core 8 MVC | Internal REST API — all business logic and data access |
| **Frontend** | Blazor Server | Public-facing web UI — the only internet-accessible service |
| **Scrape Worker** | .NET Worker Service | Background job — dequeues scrape jobs and runs Playwright |

---

## 4. Environments

The application has two environments with **completely separate Azure resource sets**. No resource is shared between environments.

| Aspect | Local (Development) | Production |
|---|---|---|
| Run method | F5 in Visual Studio — no Docker | Docker containers on Azure Container Apps |
| Authentication to Azure | Developer's VS / Azure CLI credentials (`DefaultAzureCredential`) | Managed Identity |
| SQL Server | Azure SQL Database — **dev instance** | Azure SQL Database — **prod instance** |
| Blob Storage | Azure Blob Storage — **dev storage account** | Azure Blob Storage — **prod storage account** |
| Key Vault | Azure Key Vault — **dev vault** | Azure Key Vault — **prod vault** |
| Redis | In-memory rate limiting (no Redis locally) | Azure Cache for Redis — Basic C0 tier |
| OTel export | Console exporter (stdout) | Azure Monitor / Application Insights |
| Config source | `appsettings.Development.json` + User Secrets | Environment variables injected by ACA from Key Vault references |
| HTTPS | `https://localhost:5001` (backend), `https://localhost:5000` (frontend) | ACA built-in HTTPS ingress |
| Public ingress | N/A | ACA HTTP ingress — no Application Gateway needed |
| CORS | Backend allows `https://localhost:5000` | CORS disabled — backend is internal-only |

### 4.1 Local Development — F5 Run

- All three services — Backend API, Frontend, and Scrape Worker — run as standard .NET processes. **No Docker required locally.**
- Use Visual Studio **multiple startup projects** to start all three simultaneously on F5. The Worker runs as a background console process with no HTTP port; it polls the dev Storage Queue automatically once started.
- `DefaultAzureCredential` is used for all Azure resource access locally. The developer must be signed into Visual Studio or Azure CLI with an account that has:
  - `Key Vault Secrets User` on the dev Key Vault
  - `Storage Blob Data Contributor` on the dev storage account
  - `Storage Queue Data Contributor` on the dev storage account
  - `SQL DB Contributor` (or connection string in user secrets) on the dev SQL instance
- Sensitive secrets (X OAuth credentials, JWT secret) are stored in **Visual Studio User Secrets** — never committed to source control.
- `appsettings.Development.json` contains only non-sensitive config (resource URLs, feature flags) and is committed to source control.
- Rate limiting uses **in-memory** storage locally (no Redis).
- OTel exports to **console** (stdout) by default.

### 4.2 Production Deployment

- All three services are deployed as separate Docker containers on **Azure Container Apps (ACA)**, Consumption plan.
- The Frontend has external HTTPS ingress (public). The Backend API has internal ingress only. The Worker has ingress disabled — it scales on Storage Queue depth.
- **Network isolation:** The Backend API is not reachable from the public internet. ACA internal ingress restricts access to apps within the same Container Apps Environment (`cae-mox-prod`). Only the Frontend and Worker can reach the Backend — browsers never communicate with it directly.
- ACA provides automatic TLS for the Frontend — **no Application Gateway needed.**
- Secrets injected via ACA Key Vault secret references using Managed Identity.
- No VNet required — ACA internal ingress provides sufficient isolation for this architecture.

### 4.3 Network Isolation

The Backend API is **internal-only** in production. It has no public endpoint and cannot be reached from the internet. Only the Frontend and Worker — running in the same ACA environment — can call it via ACA internal DNS (`http://ca-mox-api-prod`). This means:

- No API keys or mTLS required between Frontend and Backend — network-level isolation is sufficient.
- The user's JWT (issued by the Backend after X SSO token exchange) is forwarded by the Frontend on protected calls. Anonymous requests go through without a token.
- CORS is disabled in production since no browser-originated requests reach the Backend.

### 4.4 CORS Configuration

CORS is only needed locally. In production the backend is internal-only and never receives browser requests directly.

```csharp
// Backend Program.cs
if (env.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy("LocalDev", policy =>
            policy.WithOrigins("https://localhost:5000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    app.UseCors("LocalDev");
}
```

---

## 5. Configuration Loading Order

Both the Backend API and the Frontend resolve configuration in this priority order (highest wins):

1. Environment variables (production — injected by ACA)
2. Azure Key Vault (via `AddAzureKeyVault` with `DefaultAzureCredential`)
3. Visual Studio User Secrets (local only)
4. `appsettings.Development.json` (local — committed, non-sensitive)
5. `appsettings.json` (base defaults)

```csharp
// Program.cs
var credential = new DefaultAzureCredential();

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{env}.json", optional: true)
    .AddUserSecrets<Program>(optional: true)       // local only, no-op in prod
    .AddAzureKeyVault(new Uri(kvUri), credential)  // dev vault locally, prod vault in prod
    .AddEnvironmentVariables();                     // ACA injected vars win in prod
```

The Key Vault URI is the only bootstrapping value needed in `appsettings.json`.

---

## 6. Azure Resources

### 6.1 Service Selection (Budget-Conscious)

| Azure Service | Tier | Purpose |
|---|---|---|
| Azure Container Apps | Consumption (pay-per-use) | Host all three containers — scales to zero at idle |
| Azure SQL Database | Basic (5 DTU) or Serverless | Primary data store |
| Azure Blob Storage | LRS, Hot tier | Tweet screenshots + Storage Queue for scrape jobs |
| Azure Key Vault | Standard | Secrets management |
| Azure Cache for Redis | Basic C0 | Distributed rate-limit counters (prod only) |
| Azure Monitor / Application Insights | Pay-as-you-go | OTel telemetry sink |
| Azure Container Registry | Basic tier | Docker images |

> **Queue note:** Azure Storage Queue is part of the existing Blob Storage account — no separate service or cost. The `scrape-jobs` queue lives in the same storage account as the `screenshots` blob container.

**Deliberately excluded:**
- ~~Azure Application Gateway~~ — replaced by ACA built-in ingress
- ~~Azure Kubernetes Service~~ — replaced by Azure Container Apps
- ~~Azure Virtual Network~~ — not required for ACA Consumption plan

### 6.2 Resource Naming Convention

| Resource | Dev | Prod |
|---|---|---|
| Resource Group | `rg-mox-dev` | `rg-mox-prod` |
| SQL Server | `sql-mox-dev` | `sql-mox-prod` |
| SQL Database | `sqldb-mox-dev` | `sqldb-mox-prod` |
| Blob Storage / Queue | `stmoxdev` | `stmoxprod` |
| Storage Queue name | `scrape-jobs` | `scrape-jobs` |
| Key Vault | `kv-mox-dev` | `kv-mox-prod` |
| Redis | N/A (in-memory) | `redis-mox-prod` |
| Container Registry | N/A | `acrmoxprod` |
| Container Apps Env | N/A | `cae-mox-prod` |
| Backend ACA app | N/A | `ca-mox-api-prod` |
| Frontend ACA app | N/A | `ca-mox-frontend-prod` |
| Worker ACA app | N/A | `ca-mox-worker-prod` |
| App Insights | `appi-mox-dev` (optional) | `appi-mox-prod` |

### 6.3 Secrets Management

**Production:** All secrets in prod Key Vault. ACA apps reference them via Key Vault secret references — no secrets in container images or ACA env var literals.

**Local:** `DefaultAzureCredential` reads from the dev Key Vault. Secrets that require initial bootstrap (X OAuth credentials) go in Visual Studio User Secrets:
```
dotnet user-secrets set "XOAuth:ClientSecret" "your-secret-here" --project src/Api
```

### 6.4 Database Backup Policy

Azure SQL automated backups are on by default:
- Full backups: weekly
- Differential: every 12 hours
- Transaction log: every 5–10 minutes
- Retention: 7 days (Basic tier) — extend to 35 days via LTR policy if needed

---

## 7. Non-Functional Requirements

| Requirement | Target |
|---|---|
| Availability | 99% uptime (ACA Consumption SLA) |
| Page load (p95) | < 2 seconds |
| Tweet submission API response | < 500ms — fire-and-forget, returns `202` immediately |
| Tweet scrape time (p50) | < 30 seconds from submission to `FetchStatus='Ok'` |
| Tweet scrape time (p95) | < 2 minutes |
| Frontend polling interval | Every 3 seconds; max 2 minutes before timeout message |
| Cold start (after scale-to-zero) | 5–15s for API/Frontend; up to 30s for Worker (Chromium image) |
| Idle cost | Near zero — all three ACA apps scale to zero when unused |
| Screenshot success rate | ≥ 90% (failures are non-blocking) |
| Data retention | Permanent — no TTL on tweets or blobs |

---

## 8. Out of Scope (v1.0)

- Email notifications
- Mobile native app — the responsive web app is the mobile experience
- Progressive Web App features — offline support, home screen install, push notifications
- Bulk import of tweet URLs
- Tweet translation or language detection
- Public API for third-party consumers
- Dark/light mode toggle (default to system `prefers-color-scheme`)
- Comment or annotation system on tweets
- JWT revocation list (revocation handled by `IsActive` check on every request)
