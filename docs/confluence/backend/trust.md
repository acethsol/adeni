# Backend module — Trust & Verification

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Verification badges** beyond base tenant verification — tiered trust signals, tenant requests, admin approval queue.

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Tenancy/TenantVerificationBadge`, `VerificationBadgeType` |
| Application | `Adeni.Application/Trust/IVerificationBadgeService` |
| Infrastructure | `Adeni.Infrastructure/Trust/VerificationBadgeService` |
| API | `VerificationController` — tenant + admin routes |

Registration: `AddTrustModule()`.

---

## HTTP routes

| Method | Route |
|--------|-------|
| POST | `/api/v1/tenant/verification/badges/request` |
| GET | `/api/v1/admin/verification/badges/pending` |
| POST | `/api/v1/admin/verification/badges/{id}/approve` |

Discovery surfaces approved badges via trust signals.

---

## Clients

Portal (request badge), Admin **Trust badges** page.

---

## Related

- [Tenancy](./tenancy.md), [Discovery](./discovery.md), [Admin](./admin.md)
