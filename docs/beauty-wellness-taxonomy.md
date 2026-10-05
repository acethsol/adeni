# Adeni Beauty & Wellness — taxonomy & customer flows

> **Status:** Implemented (v1) | **Related:** [product-strategy.md](./product-strategy.md)

## Catalog source of truth

| Asset | Path |
| --- | --- |
| Categories + market featured order | `packages/shared/src/data/wellness-categories.json` |
| Service templates per category | `packages/shared/src/data/wellness-service-templates.json` |
| Operating model defaults | `packages/shared/src/data/category-workflows.json` |

API: `GET /api/v1/categories?market=lagos&wellness=true` · `GET /api/v1/categories/{slug}/service-templates`

Legacy slugs (`barbers`, `hair-salons`, …) normalize to canonical slugs for onboarding and discovery filters.

---

## How it works for **customers** (Discover)

1. **Market** — Cookie/geo picks Lagos, Ottawa, etc. Explore category order follows `marketFeaturedOrder` in the JSON catalog.
2. **Explore** — Customer taps a category (e.g. Massage & Bodywork). Only Beauty & Wellness v1 categories are shown (`wellness=true`).
3. **Results** — Businesses match if the filter slug equals their **primary** `categorySlug`, any row in `business_profile_categories`, or a **legacy** primary slug that aliases to that category.
4. **Business page** — Customer sees services (each optional `categorySlug`, `bookingDeliveryType`). Today booking still uses appointment slots; class/session/experience types are stored for later UX.
5. **Book** — Pick service → slot → confirm (Auth0 customer when enabled).

---

## How it works for **businesses** (Portal)

1. **Register** (`/register`) — Pick **market**, **primary category**, optional **also list under** checkboxes, location, slug.
2. **Services** (`/services`) — Add from **templates** or custom; set price, duration, booking type.
3. **Availability** — Weekly hours for slot generation.
4. **Verification** — Unchanged; once verified, locations appear on Discover.

See **`/setup`** in the portal for a plain-language walkthrough aimed at owners.

---

## Data model

```
BusinessProfile.categorySlug          → primary (canonical)
business_profile_categories           → primary + additional rows
ServiceOffering.categorySlug          → optional, for display/filter
ServiceOffering.catalogServiceId      → template id from JSON
ServiceOffering.bookingDeliveryType   → appointment | class | session | experience | mobile_appointment
```

---

## Implementation phases (done in this slice)

1. Wellness category catalog + hide home trades on Discover/onboarding  
2. Service templates JSON + API + portal picker  
3. Multi-category table + discovery OR-match  
4. `bookingDeliveryType` on services (storage + portal; booking UX still appointment-first)
