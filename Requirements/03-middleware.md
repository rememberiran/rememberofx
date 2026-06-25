# Memory of X — Middleware

> **Scope:** Middleware pipeline order, CorrelationId, HTTP request/response logging, load shedding, rate limiting, and logging field requirements.
> **Read alongside:** `00-overview.md`, `06-observability.md`
> **Applies to:** Backend API and Frontend (both services share the same middleware pattern)

---

## 1. Pipeline Registration Order

Register middleware in this exact order in both the Backend API and the Frontend:

```
1. CorrelationIdMiddleware        ← must be first — opens logger scope before anything else
2. HttpLoggingMiddleware          ← logs every request in and response out
3. LoadSheddingMiddleware         ← rejects requests when server is at capacity
4. UseHttpsRedirection
5. UseRateLimiter                 ← per-IP, per-policy rate limits
6. UseAuthentication
7. UseAuthorization
8. MapControllers / MapRazorComponents
```

Order is non-negotiable:
- `CorrelationIdMiddleware` before `HttpLoggingMiddleware` — the correlation ID must be in scope before the first log entry is written.
- `LoadSheddingMiddleware` before `UseRateLimiter` — capacity protection is more fundamental than per-user throttling. A shed request never consumes a rate-limit slot.

---

## 2. CorrelationIdMiddleware

**File:** `Api/Middleware/CorrelationIdMiddleware.cs`

1. Read `X-Correlation-ID` header from the incoming request.
2. If absent, generate `Guid.NewGuid().ToString()`.
3. Store in `HttpContext.Items["CorrelationId"]`.
4. Open `ILogger` scope: `logger.BeginScope(new { CorrelationId })` — this ensures every subsequent log entry in the request carries the correlation ID automatically.
5. Echo in response header: `X-Correlation-ID`.
6. Forward via `DelegatingHandler` on all outbound `HttpClient` calls (X.com navigation, Frontend → Backend calls).

---

## 3. HTTP Logging Middleware

**File:** `Api/Middleware/HttpLoggingMiddleware.cs`

Logs every inbound request and its corresponding outbound response as a structured log pair.

### Inbound Request Log (written on arrival, before the handler runs)

```csharp
_logger.LogInformation(
    "HTTP Request {Method} {Path}{QueryString} from {IpAddress}",
    context.Request.Method,
    context.Request.Path,
    context.Request.QueryString,
    ipAddress);
```

Fields on every request log entry:

| Field | Source |
|---|---|
| `Method` | HTTP method |
| `Path` | e.g. `/api/tweets/search` |
| `QueryString` | e.g. `?q=keyword&sort=votes` |
| `IpAddress` | `X-Forwarded-For` first value; fallback to `RemoteIpAddress` |
| `CorrelationId` | From logger scope (set by step 1) |
| `UserId` | `"anonymous"` at request time — updated to authenticated value in response log |
| `UserAgent` | `User-Agent` header, truncated to 200 chars |
| `ContentLength` | `Content-Length` header if present |

**Request body:** not logged. Avoids capturing auth tokens or sensitive data. Write-endpoint payloads are already captured in `AuditLog.Payload`.

### Outbound Response Log (written after handler completes)

```csharp
_logger.LogInformation(
    "HTTP Response {Method} {Path} → {StatusCode} in {ElapsedMs}ms",
    context.Request.Method,
    context.Request.Path,
    context.Response.StatusCode,
    elapsed.TotalMilliseconds);
```

Additional fields:

| Field | Source |
|---|---|
| `StatusCode` | HTTP status code integer |
| `ElapsedMs` | Time from request arrival to response flush |
| `CorrelationId` | Same as request |
| `UserId` | Resolved after auth middleware — correct for authenticated requests |

**Response body:** never logged — can contain scraped tweet text, screenshot URLs, and PII.

### Outbound HttpClient Logging (Frontend → Backend)

The Frontend's `ApiClient` registers a `LoggingDelegatingHandler`:

```csharp
// Frontend/Services/LoggingDelegatingHandler.cs
protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken ct)
{
    _logger.LogInformation("Outbound {Method} {Uri}",
        request.Method, request.RequestUri);

    var response = await base.SendAsync(request, ct);

    _logger.LogInformation("Outbound response {StatusCode} from {Uri}",
        (int)response.StatusCode, request.RequestUri);

    return response;
}
```

This ensures every Frontend-to-Backend call is logged with the same `CorrelationId` in scope.

### http.server.request Metric

`HttpLoggingMiddleware` also emits the `http.server.request` histogram after each response:

```csharp
_httpRequestHistogram.Record(elapsed.TotalMilliseconds,
    new TagList
    {
        { "method",      context.Request.Method },
        { "route",       context.GetEndpoint()?.DisplayName ?? "unknown" },
        { "status_code", context.Response.StatusCode },
        { "service",     _serviceName }
    });
```

The `route` label uses the ASP.NET route template (e.g. `/api/tweets/{id}`) — never the actual URL — to avoid high-cardinality label values from IDs.

See `06-observability.md` for the full metrics reference.

---

## 4. Load Shedding Middleware

**File:** `Api/Middleware/LoadSheddingMiddleware.cs`

Uses ASP.NET Core's built-in `RateLimiter` with a **concurrency limiter** — rejects requests when the server is already processing its maximum concurrent load, rather than queuing indefinitely.

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddConcurrencyLimiter("load-shed", o =>
    {
        o.PermitLimit = 200;   // max simultaneous in-flight requests
        o.QueueLimit  = 50;    // queue up to 50 before returning 503
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 503;
        context.HttpContext.Response.Headers["Retry-After"] = "5";
        await context.HttpContext.Response.WriteAsync(
            "Service temporarily unavailable. Please retry shortly.", ct);

        _meter.CreateCounter<long>("load_shed.rejected").Add(1);
        _logger.LogWarning("Load shedding: request rejected. CorrelationId={CorrelationId}",
            context.HttpContext.Items["CorrelationId"]);
    };
});
```

The concurrency limiter applies **globally** (not per-IP) — it protects server capacity. Per-IP rate limits (§5 below) operate independently on top.

| Config Key | Default | Description |
|---|---|---|
| `LoadShedding:PermitLimit` | `200` | Max concurrent in-flight requests |
| `LoadShedding:QueueLimit` | `50` | Max queued before 503 |

---

## 5. Rate Limiting

Uses ASP.NET Core built-in `RateLimiter` (`.NET 7+`) with per-IP policies. Rate limit state is abstracted behind an interface:

- **Local:** `IMemoryCache` — no Redis needed
- **Production:** `IDistributedCache` backed by Redis

| Policy | Scope | Limit | Returns |
|---|---|---|---|
| `anonymous-tweet-submit` | Per IP | 5 per 10 minutes | `429` |
| `vote` | Per IP | 20 per hour | `429` |
| `search` | Per IP | 60 per minute | `429` |
| `global` | Per IP | 100 per minute | `429` |

All `429` responses include a `Retry-After` header. Each triggers an increment of the `rate_limit.hit` OTel counter with label `policy={name}`.

---

## 6. Logging Field Requirements

Every log entry across both services must carry these fields (set in scope by `CorrelationIdMiddleware` and auth middleware):

| Field | Source |
|---|---|
| `CorrelationId` | `X-Correlation-ID` header or generated GUID |
| `IpAddress` | `X-Forwarded-For` first value; fallback to `RemoteIpAddress` |
| `Region` | `AZURE_PRIMARY_REGION` env var |
| `UserId` | JWT claim `sub` if authenticated; else `"anonymous"` |
| `Username` | JWT claim `username` if authenticated; else `"anonymous"` |

**Write interactions** → structured log entry + row written to `AuditLog` table (see `01-database.md §6`).  
**Read interactions** → OTel metrics counter only — not written to `AuditLog`.

---

## 7. Security Headers

Add in production only (Frontend only — the Backend API is internal):

```csharp
if (!env.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' blob: https://stmoxprod.blob.core.windows.net;";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        await next();
    });
}
```

CSP allows images from the prod blob storage account (for tweet screenshots served via SAS URLs). Adjust the CSP `img-src` if the storage account name changes.
