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
