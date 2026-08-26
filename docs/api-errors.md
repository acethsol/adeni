# API errors (RFC 7807 + i18n codes)

Adeni APIs return [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `ProblemDetails` for expected and unexpected failures.

## Response shape

```json
{
  "type": "about:blank",
  "title": "English fallback for logs and legacy clients",
  "status": 403,
  "detail": "English fallback for logs and legacy clients",
  "code": "subscription.booking_limit_reached",
  "params": { "limit": 10 },
  "correlationId": "abc123..."
}
```

| Field | Purpose |
|-------|---------|
| `code` | Stable machine-readable identifier. Primary contract for clients. |
| `params` | Optional interpolation values for client-side i18n (`{limit}`, `{resource}`, …). |
| `title` / `detail` | English fallback only — do not rely on these for UI copy. |
| `correlationId` | Request correlation ID (also in `X-Correlation-Id` response header). |

Unhandled exceptions return `500` with `code: "internal.server_error"` and no stack trace in production.

## HTTP status mapping

| Code prefix / legacy | HTTP |
|----------------------|------|
| `validation`, `validation.*` | 400 |
| `not_found`, `not_found.*` | 404 |
| `forbidden`, `auth.*`, `subscription.*`, `entitlement.*` | 403 |
| `conflict`, `conflict.*`, `booking.slot_unavailable`, `booking.slot_locked` | 409 |
| `booking.slot_expired` | 400 |
| `internal.*` | 500 |

## Client i18n pattern

1. Read `code` and optional `params` from the ProblemDetails body.
2. Translate with `@adeni/shared`:

```ts
import { localizeErrorResponse, type ApiErrorResponse } from "@adeni/shared";

const message = localizeErrorResponse(locale, errorBody);
```

`ApiErrorResponse` is the typed shape of the RFC 7807 JSON body (`code`, `params`, `correlationId`, …). It is distinct from `AdeniApiError`, the thrown exception class in `@adeni/api-client`.

Translation keys live under `errors.<code>` in locale message files (en/fr/es/pt), e.g. `errors.subscription.booking_limit_reached`.

3. Fall back to `title`, then `detail`, then raw `code` if no translation exists yet.

Web apps can use `useApiErrorMessage()` from `apps/web/lib/api-error.ts`.

## Adding a new error

1. Add a factory in `ErrorCodes` (`src/Adeni.Domain/Common/ErrorCodes.cs`) with a dotted code and optional `params`.
2. Return it from the service via `Result.Failure(...)`.
3. Add matching keys to `packages/shared/src/i18n/errors.ts` for all four locales.
4. Map HTTP status in `ApiErrorResponseMapper` if the prefix is new.

## Error code catalog (initial)

| Code | HTTP | Params |
|------|------|--------|
| `subscription.booking_limit_reached` | 403 | `limit` |
| `subscription.multi_location_required` | 403 | — |
| `booking.slot_expired` | 400 | — |
| `booking.slot_unavailable` | 409 | — |
| `booking.slot_locked` | 409 | — |
| `auth.required` | 403 | — |
| `auth.customer_required` | 403 | — |
| `auth.business_access_denied` | 403 | — |
| `internal.server_error` | 500 | — |

Legacy generic codes (`validation`, `forbidden`, `conflict`, `not_found`) remain supported until call sites are migrated.

## API versioning & deprecation (Murphy)

Source: [@mattmurphyai — API contract reel](https://www.instagram.com/reel/DcYrFHSAOhJ/). Full agent rules: [AGENTS.md](../AGENTS.md#api-contract-hardening-murphy).

### Versioning policy

- **Current version:** `v1` — all public routes live under `/api/v1/...`.
- **New endpoints:** Must use `/api/v1/` until a deliberate v2 programme starts.
- **Breaking changes:** Never ship silently. Prefer additive changes (new optional fields, new endpoints).
- **v2 introduction:** Parallel prefix `/api/v2/...`; keep v1 until sunset date passes.

### Deprecation headers (target — not yet middleware)

When an endpoint or field is scheduled for removal, responses should include:

```http
Deprecation: true
Sunset: Sat, 01 Jan 2028 00:00:00 GMT
Link: </api/v2/bookings>; rel="successor-version"
```

Clients and `@adeni/api-client` should log or surface `Deprecation` headers during staging tests.

### Mutating endpoint checklist

Before merging a new POST/PATCH/DELETE:

| Check | Requirement |
|-------|-------------|
| Auth | JWT (Auth0) for tenant/business/admin; document if intentionally anonymous |
| Tenant scope | `X-Tenant-Id` + claim match for tenant routes |
| Errors | Dotted `code` in `ErrorCodes` + i18n keys |
| Contract | Zod schema in `packages/shared`; api-client method updated |
| Idempotency | `Idempotency-Key` header for payment/booking writes where duplicate risk exists |
| Rate limit | Anonymous mutations flagged for edge rate limiting |

### Correlation

Every response includes `X-Correlation-Id` (see `CorrelationIdMiddleware`). Clients should send the same header on retries so support can trace forged or duplicate requests.
