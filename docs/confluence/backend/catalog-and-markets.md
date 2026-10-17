# Backend module — Catalog & Markets

> **Parent:** [Backend Overview](../backend-overview.md)

---

## Overview

**Product taxonomy** (beauty & wellness categories), **business types**, **capabilities** (feature flags per type), and **multi-market configuration** (currency, payment defaults, live flags).

---

## Layer map

| Layer | Path |
|-------|------|
| Domain | `Adeni.Domain/Catalog/` — `CatalogMarket` |
| Application | `Adeni.Application/Catalog/`, `Adeni.Application/Markets/` |
| Infrastructure | `Adeni.Infrastructure/Catalog/`, `Adeni.Infrastructure/Markets/` |
| API | `CategoriesController`, `MarketsController`, `AuthController.AdminMarketsController` |

Registration: `AddCatalogModule()`, `AddAdeniMarkets()`.

---

## Key services

| Port | Implementation | Role |
|------|----------------|------|
| `ICategoryService` | `CategoryService` | Category tree, Redis-cached public list |
| `IBusinessCapabilitiesService` | `BusinessCapabilitiesService` | Maps business type → capabilities[] |
| `IMarketCatalog` / `IMarketCatalogLoader` | JSON-backed loader + seeder | Markets config at startup |

Static JSON sources also mirrored in `@adeni/shared` (`wellness-categories.json`, `markets.ts`).

---

## HTTP routes (primary)

| Method | Route | Auth |
|--------|-------|------|
| GET | `/api/v1/categories` | Public |
| GET | `/api/v1/markets` | Public |
| GET/POST/PATCH | `/api/v1/admin/markets` | Admin |

---

## Client usage

- **Discover:** market context, category hero, discovery filters
- **Portal:** service templates, capability-gated nav
- **Admin:** market editor, go-live toggles

**Rule:** UI branches on **`capabilities`** from API — not hard-coded slugs (`AGENTS.md`).

---

## Caching

Categories and market-derived reads use Redis — see `docs/caching-setup.md` for key patterns.

---

## Tests

- `CategoryServiceTests`, market catalog tests, admin market integration

---

## Related docs

- `docs/markets.md`, `docs/beauty-wellness-taxonomy.md`
- [Tenancy](./tenancy.md), [Booking](./booking.md)
