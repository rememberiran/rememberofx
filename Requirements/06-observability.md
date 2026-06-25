# Memory of X — Observability

> **Scope:** OpenTelemetry setup, NuGet packages, environment-specific exporters, all custom metrics, alerting thresholds, and trace context propagation.
> **Read alongside:** `00-overview.md`, `03-middleware.md`
> **Applies to:** All three services — Backend API, Frontend, and Scrape Worker

---

## 1. NuGet Packages (all services)

```
OpenTelemetry
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.Http
OpenTelemetry.Instrumentation.SqlClient
OpenTelemetry.Exporter.OpenTelemetryProtocol    # OTLP — production
OpenTelemetry.Exporter.Console                  # Console — local dev
Azure.Monitor.OpenTelemetry.AspNetCore          # Application Insights exporter
```

---

## 2. Registration

Register OTel in each service's `Program.cs`. The exporter is chosen by environment:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddMeter("MemoryOfX"))
    .WithLogging();

if (env.IsDevelopment())
{
    // Local: write to console — no external dependency
    otelBuilder.WithTracing(t => t.AddConsoleExporter());
    otelBuilder.WithMetrics(m => m.AddConsoleExporter());
}
else
{
    // Production: Azure Monitor (Application Insights)
    builder.Services.AddAzureMonitorOpenTelemetry(o =>
        o.ConnectionString = config["ApplicationInsights:ConnectionString"]);
}
```

---

## 3. Signals

| Signal | What is captured |
|---|---|
| **Traces** | ASP.NET Core request spans, outbound HTTP calls (X.com via Playwright is not instrumented — worker logs instead), SQL queries via SqlClient instrumentation |
| **Metrics** | ASP.NET Core runtime metrics + all custom counters and histograms below |
| **Logs** | Structured `ILogger` logs exported via OTel log bridge; every entry carries `CorrelationId`, `IpAddress`, `Region`, `UserId` — see `03-middleware.md §6` |

---

## 4. Custom Metrics

All metrics are registered on a named meter `"MemoryOfX"` and carry base attributes: `service.name`, `deployment.environment`, `az.region`.

| Metric Name | Type | Labels | Description |
|---|---|---|---|
| `http.server.request` | Histogram | `method`, `route`, `status_code`, `service` | Request duration in ms — emitted by `HttpLoggingMiddleware` on every response |
| `tweet.submitted` | Counter | — | New tweet stub inserted and enqueued |
| `tweet.duplicate` | Counter | — | Duplicate tweet detected on submit |
| `tweet.not_found` | Counter | — | Worker: tweet deleted/unavailable |
| `tweet.private` | Counter | — | Worker: protected account |
| `tweet.search` | Counter | — | Search query executed |
| `folder.viewed` | Counter | — | Folder detail page viewed |
| `vote.cast` | Counter | — | Vote recorded |
| `vote.duplicate` | Counter | — | Duplicate vote attempt |
| `auth.login` | Counter | — | Successful SSO login |
| `auth.denied` | Counter | — | Denied login attempt |
| `rate_limit.hit` | Counter | `policy` | Rate limit exceeded — label names the policy |
| `load_shed.rejected` | Counter | — | Request rejected by load shedding (503) |
| `screenshot.success` | Counter | — | Screenshot captured and uploaded |
| `screenshot.failure` | Counter | — | Screenshot capture failed (non-blocking) |

### `http.server.request` — Detail

The `route` label uses the ASP.NET Core route **template** (e.g. `/api/tweets/{id}`), never the actual resolved URL. This prevents high-cardinality label explosion from individual tweet IDs.

```csharp
// HttpLoggingMiddleware — emitted after await _next(context)
var route = context.GetEndpoint()?.DisplayName ?? "unknown";
_httpRequestHistogram.Record(elapsed.TotalMilliseconds,
    new TagList
    {
        { "method",      context.Request.Method },
        { "route",       route },
        { "status_code", context.Response.StatusCode },
        { "service",     _serviceName }
    });
```

This single histogram gives you:
- Request rate per endpoint (count over time)
- Error rate per endpoint (`status_code >= 400` / total)
- Latency percentiles per endpoint (p50, p95, p99)
- Status code distribution per endpoint

---

## 5. Trace Context Propagation

- W3C `traceparent` header propagated on all inter-service HTTP calls (Frontend → Backend, Backend → X API).
- `CorrelationId` added as span attribute `app.correlation_id` on every trace.
- `UserId` added as span attribute `app.user_id`.

The `CorrelationId` ties a trace to its `AuditLog` row and its structured log entries, enabling end-to-end request reconstruction across all three services.

---

## 6. Alerting Thresholds

Configure in Azure Monitor on the prod Application Insights instance. All alerts notify the email address in `ALERT_EMAIL` env var on the ACA environment.

| Alert | Condition | Severity | Action |
|---|---|---|---|
| High overall error rate | `http.server.request` with `status_code >= 500` > 5% of total over 5 min | Sev 2 | Email |
| Load shedding active | `load_shed.rejected` > 20 per minute | Sev 2 | Email — server under extreme load |
| Per-endpoint error spike | `http.server.request` `status_code >= 500` for any single `route` > 10 in 1 min | Sev 2 | Email |
| High latency | `http.server.request` p95 > 5 seconds over 5 min | Sev 3 | Email |
| Rate limit spike | `rate_limit.hit` > 100 per minute | Sev 3 | Email |
| Screenshot failure spike | `screenshot.failure` > 10 per hour | Sev 3 | Email |
| Health probe failure | ACA health check failures > 0 for 2 consecutive minutes | Sev 1 | Email |
| DB connection errors | `dependencies/failed` where `type=SQL` > 3 per minute | Sev 2 | Email |
| Cold start latency | `http.server.request` p95 > 20 seconds | Sev 3 | Informational |
