# Adeni Beauty & Wellness — category taxonomy

> **Status:** Implemented (v1 catalog + portal/discover) | **Last updated:** October 2026  
> **Related:** [product-strategy.md](./product-strategy.md) §2–§3.5, [markets.md](./markets.md), [target-client-architecture.md](./target-client-architecture.md)

## Why this doc exists

Adeni’s first vertical is **Beauty, Wellness & Self-Care** — marketed internally as **Adeni Beauty & Wellness**. That is intentionally broader than “clinical wellness” and narrower than “every local service.” It aligns with how customers search (spa + hair + nails + massage) and with platforms like Zenoti, without pulling us into regulated healthcare in v1.

Ottawa and Lagos share **one taxonomy and one codebase**. **Market config** controls which categories are promoted, featured, and eligible for onboarding — not separate product forks.

---

## Catalog source of truth (runtime)

| Asset | Path |
| --- | --- |
| Categories + market featured order | `packages/shared/src/data/wellness-categories.json` |
| Service templates per category | `packages/shared/src/data/wellness-service-templates.json` |
| Operating model defaults | `packages/shared/src/data/category-workflows.json` |

The file is the catalog on purpose. Discover, the portal, and the API all read `packages/shared`, and a business only stores a category slug. `catalog.markets` is copied into Postgres so discovery can filter locations in SQL. Categories are not. Set `enabled` to `false` to hide a category from Explore and onboarding; restart the API so the cache reloads.

API: `GET /api/v1/categories?market=lagos&wellness=true` · `GET /api/v1/categories/{slug}/service-templates`

Legacy slugs (`barbers`, `hair-salons`, …) normalize to canonical slugs for onboarding and discovery filters.

---

## Consumer-facing Explore

Categories are **browse/filter chips**, not deep trees. Each category owns a **flat list of bookable services** (templates businesses can adopt or rename).

`enabled` in `wellness-categories.json` is the live switch. Version notes stay in this document; the API does not branch on a version name.

| Explore label | Slug | Enabled |
| --- | --- | --- |
| Massage & Bodywork | `massage-bodywork` | ✅ |
| Spa & Relaxation | `spa-relaxation` | ✅ |
| Skincare & Aesthetics | `skincare-aesthetics` | ✅ |
| Hair & Grooming | `hair-grooming` | ✅ |
| Nails | `nails` | ✅ |
| Fitness | `fitness` | ✅ |
| Yoga | `yoga` | ✅ |
| Pilates | `pilates` | ✅ |
| Recovery & Performance | `recovery-performance` | 🟡 (catalog only; not in onboarding picker) |
| Holistic Wellness | `holistic-wellness` | 🟡 |
| Nutrition & Wellness Coaching | `nutrition-coaching` | 🟡 |
| Sauna & Thermal | `sauna-thermal` | 🟡 |
| Classes & Experiences | `wellness-experiences` | 🟡 |

**Defer (🔴 later)** — licensing, clinical records, health data, privacy regimes: medical aesthetics, physio, chiropractic, mental health, registered dietitians.

**Out of wedge:** home trades (`plumbers`, `electricians`, …) stay in the catalog for quote demos but are hidden from Explore and onboarding (`enabled: false`, or `wellness=false` plus `includeDisabled=true` to list them).

### Example service lists (catalog templates, not subcategories)

**Massage & Bodywork** — Swedish, deep tissue, sports, couples, prenatal, hot stone, reflexology, …  

**Hair & Grooming** — haircut, barbering, braids, locs, styling, color, treatments, beard grooming, …  

---

## Three axes (do not conflate)

| Axis | Purpose | Examples |
| --- | --- | --- |
| **Discovery category** | What customers browse; SEO/filter | `hair-grooming`, `massage-bodywork` |
| **Business type** | Operating model | `scheduled_appointment`, `quote_request` |
| **Booking delivery type** | How fulfillment works | `appointment`, `class`, `session`, `experience`, `mobile_appointment` |

**Booking delivery type** attaches to **services** (drives future calendar UX):

```
Category (many per business)
    ↓
Service (template + tenant offering)
    ↓
Booking delivery type
    ↓
Availability / capacity rules
    ↓
Practitioner or resource (future)
```

v1 stores delivery type on `service_offerings`; booking UX is still **appointment-first**.

---

## Business ↔ category (many-to-many)

```
Business (tenant)
├── BusinessProfile.CategorySlug     — primary (canonical)
├── business_profile_categories      — primary + additional rows
└── service_offerings                — optional categorySlug, catalogServiceId, bookingDeliveryType
```

Discovery matches if **any** category matches the filter (plus legacy alias on primary slug).

---

## Market-specific prominence

Implemented via `marketFeaturedOrder` in `wellness-categories.json` (Lagos vs Ottawa chip order). Future: optional `hiddenCategorySlugs` on market config.

---

## Legacy slug mapping

| Legacy slug | Canonical slug |
| --- | --- |
| `barbers`, `hair-salons` | `hair-grooming` |
| `nail-spa` | `nails` |
| `makeup-brows` | `skincare-aesthetics` |

---

## How it works for **customers** (Discover)

1. **Market** — Cookie/geo; Explore order from `marketFeaturedOrder`.
2. **Explore** — Enabled wellness categories only.
3. **Results** — Primary, additional categories, or legacy primary slug.
4. **Business page** — Services with optional category and delivery type.
5. **Book** — Service → slot → confirm.

---

## How it works for **businesses** (Portal)

1. **`/register`** — Primary category + optional “also list under” + market.
2. **`/services`** — Templates or custom services + booking type.
3. **`/setup`** — Owner-facing explanation of the customer journey.
4. **Availability + verification** — unchanged.

---

## PostgreSQL tables

| Schema | Table | Role |
| --- | --- | --- |
| `tenancy` | `business_profiles` | Primary `CategorySlug` |
| `tenancy` | `business_profile_categories` | Primary + additional (`IsPrimary`) |
| `booking` | `service_offerings` | `CategorySlug`, `CatalogServiceId`, `BookingDeliveryType` |

Migration: `20261005181138_WellnessTaxonomy`.

---

## Still to do

- Class/capacity booking UX for `class` / `experience` delivery types  
- Regulated 🔴 categories (compliance track)  
- ~~Normalize dev seed data to canonical slugs~~ ✅ Lagos + Ottawa, enabled categories only
- Optional market-level `hiddenCategorySlugs` in `markets.json`

---

## Positioning reminder

External copy should say **beauty & wellness** (or **self-care**), not “wellness app only,” when hair, nails, and aesthetics are in scope.
