# Backend module — Admin Operations

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Internal Adeni staff** capabilities: business verification queue, customer privacy (export/erasure), market administration, review moderation, subscription/revenue analytics, transaction search, tenant commercial terms.

---

## Layer map

| Layer | Path |
|-------|------|
| Application | `Adeni.Application/Admin/` — business, customer, market, metrics, transactions |
| Infrastructure | `Adeni.Infrastructure/Admin/` |
| API | Controllers under `/api/v1/admin/*` (split across `AuthController` partial classes, dedicated admin controllers) |

Registration: `AddAdminModule()`.

---

## HTTP routes (index)

| Route | Purpose |
|-------|---------|
| `/api/v1/admin/businesses` | Directory, approve/reject, commercial terms |
| `/api/v1/admin/customers` | Customer search, export, delete |
| `/api/v1/admin/markets` | Market CRUD, go-live |
| `/api/v1/admin/reviews` | Moderation |
| `/api/v1/admin/metrics/revenue` | Revenue metrics |
| `/api/v1/admin/metrics/subscription` | MRR / subscription metrics |
| `/api/v1/admin/transactions` | Payment transaction search |
| `/api/v1/admin` | Diagnostics (restricted) |

---

## Cross-cutting requirements

- **Audit:** all mutations via `AuditMiddleware` → audit log store
- **MFA:** admin JWT must include `amr: mfa` when policy enabled
- **PII:** export flows logged; mask in non-admin contexts

---

## Clients

**Admin app** exclusively — separate Auth0 SPA and recommended separate production origin.

---

## Tests

- `AdminBusinessServiceTests`, `AdminCustomerServiceTests`, tenant dashboard/admin integration tests

---

## Related

- [Tenancy](./tenancy.md), [Trust](./trust.md), [Subscriptions](./subscriptions.md)
