# Observability

Operational monitoring for Adeni API and clients.

**Source of truth:** [Observability v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/27230210) — this file is a repo mirror; update Confluence first, then sync here.

Adeni uses a **dual ops stack**: **Azure Application Insights** for platform operations and **Sentry** for client-side error debugging. **PostHog** covers product analytics and session replay (not crash tracking). Tool choices are frozen in [tooling-policy.md](./tooling-policy.md) — do not add Datadog/ELK/FullStory without updating that doc.

## Dual stack — Application Insights + Sentry

| Tool | Primary role | Adeni surfaces |
|------|----------------|----------------|
| **Application Insights** | Platform ops — health, traces, dependencies, SLOs, Azure alerts, SOC 2 monitoring | .NET API; optional server-side web if needed |
| **Sentry** | Developer workflow — grouped JS/Flutter errors, releases, source maps, crash context | Angular discover/portal/admin; Flutter consumer (when live) |
| **PostHog** | Product learning — funnels, session replay | Discover first; portal when useful — see [tooling-policy.md](./tooling-policy.md) |

**Why App Insights + Sentry:** App Insights fits Azure deployment, availability tests, and API dependency alerts. Sentry is stronger for Angular/Flutter stack traces and release regressions. Neither replaces PostHog for “why did users abandon booking?”

### Correlation between tools

Use one **`X-Correlation-Id`** per user action end-to-end:

1. Web/mobile generate a UUID at the start of an action (page load, booking submit, etc.).
2. `@adeni/api-client` sends it on every API request (already supported server-side).
3. App Insights traces and Serilog logs include the same ID (API middleware + `LogContext`).
4. Sentry events attach the same value as tag **`correlationId`** (or breadcrumb).

When debugging a Sentry crash, search App Insights **Transaction search** or logs for that ID to see the matching API trace.

### Alert ownership (avoid duplicate pages)

| Alert type | Owner | Examples |
|------------|--------|----------|
| Infrastructure / API | **App Insights** | Health probe failures, 5xx rate, Postgres/Redis down, cross-tenant 403 spike |
| Client regressions | **Sentry** | New error type after release, crash-free session drop, error spike per release |
| Latency SLOs | **App Insights** | p95 discovery/booking (see SLOs below) |

Do not configure both tools to page on-call for the same failure mode (e.g. API 5xx → App Insights only).

### PII and sampling

- Same rules as server logging: **no phone, email, or message bodies** in App Insights custom properties or Sentry breadcrumbs/extra context.
- **Sentry (prod):** sample performance transactions (e.g. 10–20%); keep **100% of errors**.
- **App Insights (prod):** rely on default adaptive sampling for traces; watch ingestion if browser RUM volume grows.

### Rollout by client

| Client | Phase 1 (platform) | Phase 2 (client errors) | Product learning |
|--------|--------------------|-------------------------|------------------|
| **API** | App Insights via `APPLICATIONINSIGHTS_CONNECTION_STRING` | — | — |
| **Discover / Portal / Admin (Angular)** | Correlation ID via `@adeni/api-client` | Sentry browser SDK + source maps in CI | PostHog on Discover first |
| **Flutter (consumer)** | Correlation ID on API calls | Sentry Flutter SDK + `correlationId` tag | PostHog when consumer app ships |

Local dev: tools optional — Serilog console; Sentry/PostHog DSNs disabled by default.

## Health endpoint contract

**URL:** `GET /health` (anonymous)

**Response 200:**

```json
{
  "status": "healthy | degraded",
  "service": "adeni-api",
  "checks": {
    "api": "healthy",
    "database": "healthy | unhealthy | not_configured",
    "cache": "healthy | unhealthy | memory_fallback"
  }
}
```

- **`healthy`** — all checks pass (or DB not configured in dev).
- **`degraded`** — database or Redis unhealthy when configured.

**External probes (staging/prod):** Ping every 1 minute; alert after 3 consecutive failures. Use Azure Application Insights **Availability tests** or Better Stack.

## Correlation IDs

Every API request:

1. Accepts `X-Correlation-Id` or generates a GUID.
2. Returns the same value on `X-Correlation-Id` response header.
3. Pushes `correlationId` into Serilog `LogContext` for all logs in the request.
4. Stores on `admin.audit_logs.correlation_id`.

**Clients (Angular / Flutter):** Generate a UUID per user action and send `X-Correlation-Id` on API calls. Attach the same ID to Sentry (and PostHog where useful) when enabled.

## Logging

| Environment | Sink |
|-------------|------|
| Local dev | Serilog → console |
| Staging/prod | Serilog → console + Azure Application Insights (via OpenTelemetry when enabled) |

**PII:** Phone, email, and message bodies are never logged (`PiiMasker`, destructuring policy).

## OpenTelemetry (Azure Monitor)

Disabled locally by default. Enable in staging/prod:

```json
"Observability": {
  "Enabled": true,
  "ServiceName": "adeni-api",
  "ConnectionString": "<Application Insights connection string>"
}
```

Or set environment variable `APPLICATIONINSIGHTS_CONNECTION_STRING`.

Instruments: ASP.NET Core requests, outbound HTTP. Traces appear in Application Insights **Transaction search**.

### Sentry (clients)

Not required locally. In staging/prod, configure per Angular app (and Flutter when live):

| App | Env vars (example) |
|-----|---------------------|
| Discover / Portal / Admin | `SENTRY_DSN` (or app-specific public DSN), `SENTRY_AUTH_TOKEN` for CI source maps |
| Flutter | Sentry DSN via `--dart-define` / env; upload symbols in CI |

Tag every event with `correlationId` when an API call was in flight. Link releases to git SHA in CI for regression detection.

### PostHog (product analytics)

See [tooling-policy.md](./tooling-policy.md). Not a substitute for Sentry. Mask PII; separate staging vs production projects.

## SLOs (MVP)

| SLO | SLI | Target |
|-----|-----|--------|
| API availability | External `/health` success rate | 99.5% / 30 days |
| Discovery latency | p95 `GET /api/v1/discovery` | < 500 ms |
| Booking latency | p95 `POST /api/v1/bookings` | < 1 s |
| Error rate | 5xx / total requests | < 1% (5 min alert) |

## Alert rules (staging/prod)

| Alert | Condition |
|-------|-----------|
| Health check failed | Synthetic test fails 3× in 3 min |
| High error rate | 5xx > 1% over 5 min |
| Cross-tenant spike | 403 tenant mismatch > 10/hour |
| Dependency down | DB or Redis health = unhealthy |

## Local verification

```powershell
curl -i http://localhost:5169/health
# Note X-Correlation-Id on response

curl -H "X-Correlation-Id: demo-123" http://localhost:5169/health
# Response header should echo demo-123
```

## Roadmap

| Phase | Work |
|-------|------|
| **Obs 0** ✅ | Correlation ID + LogContext + OTel skeleton |
| **Obs 1** | App Insights connection string on Azure App Service (API) |
| **Obs 2** | External availability tests + platform alert rules (App Insights) |
| **Obs 3a** | Propagate `X-Correlation-Id` from `@adeni/api-client` on all Angular apps |
| **Obs 3b** | Sentry on Discover (+ portal/admin) with `correlationId` tag and release/source maps |
| **Obs 3c** | PostHog on Discover (funnels + replay; PII masking) — [tooling-policy.md](./tooling-policy.md) |
| **Obs 4** | SLO workbook + public status page (App Insights) |
| **Obs 5** | Sentry (+ optional PostHog) on Flutter consumer |
