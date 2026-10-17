# Backend module — Discovery

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Public search and geo discovery** of verified businesses. Read-heavy module that composes tenancy profiles, ratings, trust signals, and location data without violating module boundaries.

---

## Layer map

| Layer | Path |
|-------|------|
| Application | `Adeni.Application/Discovery/` — `IDiscoveryService`, sort options |
| Infrastructure | `Adeni.Infrastructure/Discovery/` — `DiscoveryService`, `DiscoverySearchExecutor`, SQL search |
| API | `DiscoveryController` — `/api/v1/discovery`, public business endpoints under `/api/v1/businesses` |

Registration: `AddDiscoveryModule()`.

---

## Key behaviors

- Returns only **Verified** tenants.
- Supports keyword, category, geo radius, sort (distance, rating, etc.).
- **Trust signals** denormalized for card display (`DiscoveryTrustSignals`).
- Heavy queries may use **stored procedures** / dedicated SQL executor for SQL Server.

---

## HTTP routes

| Method | Route | Auth |
|--------|-------|------|
| GET | `/api/v1/discovery` | Public |
| GET | `/api/v1/businesses/{slug}` | Public (masked phone) |
| GET | `/api/v1/businesses/{slug}/reviews` | Public |
| GET | `/api/v1/businesses/{slug}/staff` | Public |

---

## Caching

Profile and discovery responses use Redis keys documented in `docs/caching-setup.md`. Invalidate on profile/media updates.

---

## Clients

**Discover app** — home hero, map, search, business profile pages.

---

## Tests

- `DiscoveryServiceTests`, `DiscoverySearchExecutorTests`, `DiscoveryIntegrationTests`

---

## Related docs

- [Tenancy](./tenancy.md), [Reviews](./reviews.md), [Trust](./trust.md)
