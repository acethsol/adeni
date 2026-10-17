# Backend module — Tenancy

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Multi-tenant business lifecycle:** registration, profile, locations, verification documents, public page configuration, and **tenant-scoped customer records** (CRM-lite).

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Tenancy/` — `Tenant`, `BusinessProfile`, `BusinessLocation`, `TenantCustomer`, verification enums |
| Application | `Adeni.Application/Tenancy/` — `IBusinessOnboardingService`, `ITenantCustomerService` |
| Infrastructure | `Adeni.Infrastructure/Tenancy/` |
| API | `TenantController`, `TenantCustomersController`, `DiscoveryController` (public profile) |

Registration: `AddTenancyModule()`.

---

## Key entities

| Entity | Scope | Notes |
|--------|-------|-------|
| `Tenant` | Global | Status: pending → verified; links to subscription/billing |
| `BusinessProfile` | Tenant | Slug, display name, categories, policies, public page template |
| `BusinessLocation` | Tenant | Geo for discovery |
| `TenantCustomer` | Tenant | Per-business customer file (Sprint 35) |
| `VerificationDocument` | Tenant | Admin review queue |

All tenant data implements **`ITenantEntity`** where applicable — EF global filters apply.

---

## Key behaviors

- **Onboarding:** business registration creates tenant + owner user.
- **Verification:** admin approves/rejects; only **Verified** tenants appear in discovery.
- **Public profile:** phone masked on anonymous reads; full detail for authorized staff.
- **Category sync:** `BusinessProfileCategorySync` aligns catalog selections with profile.

---

## HTTP routes (primary)

| Method | Route | Notes |
|--------|-------|-------|
| GET/PATCH | `/api/v1/tenant` | Profile, settings, policies, public page |
| GET/POST/PATCH | `/api/v1/tenant/customers` | Tenant CRM |
| GET | `/api/v1/businesses/{slug}` | Public profile (Discover) |

Header: **`X-Tenant-Id`** on tenant mutations.

---

## Dependencies

- **Catalog** — categories, business type, capabilities
- **Storage** — cover/gallery images
- **Subscriptions** — plan gating on features
- **Admin** — verification queue

---

## Clients

| App | Features |
|-----|----------|
| Portal | Profile, locations, public page, customers |
| Discover | Public business pages |
| Admin | Business directory, verification |

---

## Tests

- `TenantCustomerServiceTests`, `TenantIsolationIntegrationTests`, onboarding integration tests

---

## Related docs

- `docs/tenant-isolation.md`
- [Catalog & Markets](./catalog-and-markets.md)
