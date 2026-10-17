# Backend module — Translation

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Server-side translation** for user-generated or dynamic strings (optional external provider). Rate-limited public endpoint for Discover i18n.

---

## Layer map

| Layer | Path |
|-------|------|
| Application | `Adeni.Application/Translation/ITranslationService` |
| Infrastructure | `Adeni.Infrastructure/Translation/TranslationService` |
| API | `TranslationController` — `/api/v1/translate` |

Registration: `AddAdeniTranslation(configuration, environment)`.

---

## Behavior

- Batches text for translation with caching where configured.
- Staging/production rate limits apply (see rate limiting in API startup).

---

## Clients

Discover (dynamic copy), potentially portal for multi-language markets.

---

## Tests

- `TranslationServiceTests`

---

## Related

- `@adeni/shared` i18n error keys (static), markets config
