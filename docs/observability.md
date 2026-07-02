# Observability

Operational monitoring for Adeni API and clients. Confluence: [Observability v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065) (see also Frontend Architecture for client RUM).

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

**Clients (Next.js / Expo):** Generate a UUID per user action and send `X-Correlation-Id` on API calls.

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
| **Obs 1** | App Insights connection string on Azure App Service |
| **Obs 2** | External availability tests + alert rules |
| **Obs 3** | Sentry on Next.js / Expo + header propagation |
| **Obs 4** | SLO workbook + public status page |
