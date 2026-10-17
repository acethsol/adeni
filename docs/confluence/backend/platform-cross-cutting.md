# Backend — Platform cross-cutting

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

Capabilities that span modules: **domain events**, **auditing**, **caching**, **persistence**, **observability**, **rate limiting**, **errors**.

---

## Domain events

| Component | Path |
|-----------|------|
| `IDomainEvent` | `Adeni.Domain/Events/` |
| `IDomainEventDispatcher` | Application + `DomainEventDispatcher` |
| `IDomainEventCollector` | EF SaveChanges integration |
| Handlers | Per-module (Notifications, Payments, …) |

Registration: `AddAdeniDomainEvents()`.

Start in-process; extract to message bus when a module extracts.

---

## Auditing

| Component | Role |
|-----------|------|
| `IAuditLogWriter` | Persists admin/ sensitive actions |
| `AuditMiddleware` | Captures `/api/v1/admin/*` mutations |
| `AuditEntry` / `AuditLogRecord` | Domain + persistence |

---

## Caching & locks

| Component | Role |
|-----------|------|
| `ICacheService` | Redis get/set, invalidation patterns |
| Distributed locks | Booking slot contention |

See `docs/caching-setup.md`.

---

## Persistence

| Component | Role |
|-----------|------|
| `AdeniDbContext` | EF Core, global tenant filters |
| `db/Adeni.Database` | Declarative SQL Server schema |
| `DevelopmentDataSeeder` | Dev-only sample data |

See `docs/database-setup.md`.

---

## Observability

| Component | Role |
|-----------|------|
| Serilog + PII destructuring | Structured logs |
| `CorrelationIdMiddleware` | Request tracing |
| App Insights / health checks | `docs/observability.md` |

---

## API errors

- `GlobalExceptionHandler`, `ApiErrorResponseMapper`
- Stable codes in `ErrorCodes` + `@adeni/shared` locales
- `docs/api-errors.md`

---

## Rate limiting

Applied in Staging/Production for webhooks, auth/sync, translate — configured in API startup (`AddAdeniRateLimiting`).

---

## Tests

- `DomainEventDispatcherTests`, persistence/tenant filter tests, middleware tests

---

## Related

- [Identity & Auth](./identity-and-auth.md) — middleware ordering
- `AGENTS.md` security non-negotiables
