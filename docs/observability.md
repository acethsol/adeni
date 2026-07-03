# Observability

Operational monitoring for Adeni API and clients.

**Source of truth:** [Observability v1 (Confluence)](https://aceth.atlassian.net/wiki/spaces/SD/pages/27230210) — this file is a repo mirror; update Confluence first, then sync here.

Adeni uses a **dual stack**: **Azure Application Insights** for platform operations and **Sentry** for client-side error debugging. Each tool has a clear scope so signals complement rather than duplicate each other.

## Dual stack — Application Insights + Sentry

| Tool | Primary role | Adeni surfaces |
|------|----------------|----------------|
| **Application Insights** | Platform ops — health, traces, dependencies, SLOs, Azure alerts, SOC 2 monitoring | .NET API, Next.js server, optional browser RUM |
| **Sentry** | Developer workflow — grouped JS/React Native errors, releases, source maps, crash context | Expo mobile, optional Next.js browser |

**Why both:** App Insights is already wired on the API (OpenTelemetry + Azure Monitor) and fits Azure deployment, availability tests, and cross-tenant alert rules. Sentry is stronger for Expo and frontend stack traces where App Insights mobile/browser UX is weaker.

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

| Client | Phase 1 (platform) | Phase 2 (client errors) |
|--------|--------------------|-------------------------|
| **API** | App Insights via `APPLICATIONINSIGHTS_CONNECTION_STRING` | — |
| **Web (Next.js)** | App Insights server instrumentation + correlation ID | Sentry browser SDK (optional; source maps in CI) |
| **Mobile (Expo)** | API-side errors only; correlation ID on API calls | Sentry React Native SDK + `correlationId` tag |

Local dev: neither tool required — Serilog console + optional Sentry DSN disabled by default.

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

**Clients (Next.js / Expo):** Generate a UUID per user action and send `X-Correlation-Id` on API calls. Attach the same ID to Sentry events when Sentry is enabled (see dual stack above).

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

Not required locally. In staging/prod, configure per app:

| App | Env vars (example) |
|-----|---------------------|
| Expo | `EXPO_PUBLIC_SENTRY_DSN`, `SENTRY_AUTH_TOKEN` (CI uploads for source maps) |
| Next.js | `SENTRY_DSN`, `SENTRY_AUTH_TOKEN` |

Tag every event with `correlationId` when an API call was in flight. Link releases to git SHA in CI for regression detection.

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
| **Obs 3a** | App Insights on Next.js (server + optional browser) + propagate `X-Correlation-Id` from `@adeni/api-client` |
| **Obs 3b** | Sentry on Expo (+ optional Next.js browser) with `correlationId` tag and release/source maps |
| **Obs 4** | SLO workbook + public status page (App Insights) |
