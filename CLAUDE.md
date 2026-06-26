# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
dotnet build                                    # Build all projects (warnings are errors)
dotnet test                                     # Run all tests
dotnet test tests/Api.Tests --filter "ClassName" # Run specific test class
dotnet format --verify-no-changes               # Check formatting without changing
dotnet format                                   # Auto-fix formatting
```

```bash
# EF Core migrations
dotnet ef migrations add MigrationName --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

## Architecture

**Memory of X** is a .NET 8 clean architecture app with strict layering:

```
Storage Records  →  Domain Mappers  →  Services (Result<T>)  →  DTO Mappers  →  Controllers
   (EF Core)      (Domain/Mappers/)    (Application/Services/)   (Api/Mappers/)   (Api/Controllers/)
```

### Key rule: Services return domain entities, not DTOs

Services return `Result<DomainEntity>`. Controllers call DTO mappers before returning HTTP responses. This keeps business logic decoupled from presentation and makes services reusable across API, Worker, and Frontend.

**Example flow:** `UserRecord` → `UserMapper.ToDomain()` → `UserService` returns `Result<User>` → `AdminController` calls `UserDtoMapper.ToDto()` → HTTP 200 with `UserDto`

### Projects

| Project | Layer | Purpose |
|---------|-------|---------|
| `Storage` | Persistence | EF Core record types matching DB schema |
| `Domain` | Domain | Entities, enums, bidirectional mappers (Record ↔ Entity) |
| `Application` | Business | Services, interfaces, `Result<T>`, `IAsyncContext<T>`, commands/queries |
| `Infrastructure` | Infrastructure | `AppDbContext`, blob storage, queue, X API client |
| `Api` | Presentation | Controllers, DTO mappers, middleware, JWT auth |
| `Worker` | Background | Scrape job processor (Playwright/Chromium) |
| `Frontend` | UI | Blazor Server, calls Backend API via typed HttpClient |

**Dependency flow:** Api → Application → Domain → Storage; Infrastructure → Application + Domain + Storage

### Result Pattern

All service methods return `Result<T>` instead of throwing exceptions. `DomainError` carries a code, message, and HTTP status. Controllers use `result.ToActionResult()` for failures and explicit DTO mapping for success.

```csharp
// Service
var record = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
if (record is null)
{
    return Result.Failure<User>(DomainError.NotFound("User not found"));
}
return Result.Success(UserMapper.ToDomain(record));

// Controller
var result = await _userService.GetAsync(id, ct);
if (!result.IsSuccess)
{
    return result.ToActionResult();
}
return Ok(UserDtoMapper.ToDto(result.Value!));
```

### Async Context

`IAsyncContext<T>` (backed by `AsyncLocal<T>`) propagates request-scoped data through async call chains without parameter passing. Set in middleware, injectable anywhere:

- `IAsyncContext<CorrelationContext>` — request correlation ID (set by `CorrelationIdMiddleware`)
- `IAsyncContext<IdentityContext>` — user ID, username, email, IP (set by `IdentityMiddleware`)

### Middleware Pipeline Order

`CorrelationIdMiddleware` → `ExceptionHandler` → `HttpLoggingMiddleware` → `RateLimiter` → `HttpsRedirection` → CORS (dev only) → `Authentication` → `Authorization` → `IdentityMiddleware` → `MapControllers`

### Authentication

JWT Bearer (HS256). Frontend handles X OAuth 2.0 PKCE, sends X access token to `POST /api/auth/token`. Backend validates with X API, looks up user, issues application JWT. Backend is internal-only (ACA internal ingress) — not reachable from the internet.

## Code Style

Enforced by three analyzer layers with `TreatWarningsAsErrors=true`:

- **Built-in .NET analyzers** (`AnalysisLevel=latest-all`)
- **StyleCop.Analyzers** (1.2.0-beta.556)
- **Meziantou.Analyzer** (2.0.182)

### Conventions

- File-scoped namespaces: `namespace X;`
- Private fields: `_camelCase`
- Braces required on all control flow blocks (SA1503 enforced)
- Trailing commas in multi-line initializers (SA1413)
- Parameters spanning multiple lines: each on its own line (SA1116/SA1117)
- No XML documentation comments (SA1600-SA1651 disabled)
- No file headers (SA1633 disabled)
- Multiple related types per file allowed (SA1402 disabled)
- `ConfigureAwait(false)` not needed (CA2007/MA0004 disabled)
- Using directives alphabetically ordered, outside namespace

### Key suppressions

CA1031 (general catch at boundaries), CA1062 (arg validation — DI handles it), CA1515 (public types for ASP.NET DI), CA2227 (EF nav property setters), CA1848/CA1873 (LoggerMessage — adopt incrementally)

## Adding New Features

### New entity: Storage record → Domain entity → Mapper → DbSet → Service → Controller → DTO mapper

1. `src/Storage/XxxRecord.cs` — EF record with properties
2. `src/Domain/Entities/Xxx.cs` — domain entity
3. `src/Domain/Mappers/XxxMapper.cs` — static `ToDomain()`/`ToRecord()` methods
4. Add `DbSet<XxxRecord>` to `IAppDbContext` and `AppDbContext.OnModelCreating()`
5. `src/Application/Interfaces/IXxxService.cs` + `src/Application/Services/XxxService.cs`
6. Register in `ServiceCollectionExtensions.AddApplicationServices()`
7. `src/Api/Mappers/XxxDtoMapper.cs` + `src/Api/Controllers/XxxController.cs`

## Key Files

| File | Purpose |
|------|---------|
| `Directory.Build.props` | Global analyzer config, TreatWarningsAsErrors |
| `.editorconfig` | Code style rules, naming, analyzer suppressions |
| `src/Api/Program.cs` | Middleware pipeline, DI, auth, OpenTelemetry |
| `src/Api/Extensions/ServiceCollectionExtensions.cs` | All DI registrations |
| `src/Application/Result.cs` | Result/DomainError types |
| `src/Application/AsyncContext.cs` | IAsyncContext/AsyncContext types |
| `src/Infrastructure/Data/AppDbContext.cs` | EF Core config (indexes, constraints, FKs) |
| `infra/main.bicep` | Azure infrastructure (ACA, SQL, Storage, Key Vault) |
