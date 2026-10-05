# Adeni Beauty & Wellness — category taxonomy

> **Status:** Design (not fully implemented) | **Last updated:** October 2026  
> **Related:** [product-strategy.md](./product-strategy.md) §2–§3.5, [markets.md](./markets.md), [target-client-architecture.md](./target-client-architecture.md)

## Why this doc exists

Adeni’s first vertical is **Beauty, Wellness & Self-Care** — marketed internally as **Adeni Beauty & Wellness**. That is intentionally broader than “clinical wellness” and narrower than “every local service.” It aligns with how customers search (spa + hair + nails + massage) and with platforms like Zenoti, without pulling us into regulated healthcare in v1.

Ottawa and Lagos share **one taxonomy and one codebase**. **Market config** controls which categories are promoted, featured, and eligible for onboarding — not separate product forks.

---

## Consumer-facing “Explore” (v1)

Categories are **browse/filter chips**, not deep trees. Each category owns a **flat list of bookable services** (templates businesses can adopt or rename).

| Explore label | Slug (proposed) | V1 |
| --- | --- | --- |
| Massage & Bodywork | `massage-bodywork` | ✅ |
| Spa & Relaxation | `spa-relaxation` | ✅ |
| Skincare & Aesthetics | `skincare-aesthetics` | ✅ |
| Hair & Grooming | `hair-grooming` | ✅ |
| Nails | `nails` | ✅ |
| Fitness | `fitness` | ✅ |
| Yoga & Pilates | `yoga-pilates` | ✅ |
| Recovery & Performance | `recovery-performance` | 🟡 |
| Holistic Wellness | `holistic-wellness` | 🟡 |
| Nutrition & Wellness Coaching | `nutrition-coaching` | 🟡 |
| Sauna & Thermal | `sauna-thermal` | 🟡 |
| Classes & Experiences | `wellness-experiences` | 🟡 |

**Defer (🔴 later)** — valuable supply, but they introduce licensing, clinical records, health data, and privacy regimes we should not own in v1:

- Medical aesthetics / medspas  
- Physiotherapy & rehabilitation  
- Chiropractic  
- Mental health (therapy, counselling)  
- Registered dietitians / clinical nutrition  

**Out of wedge (hidden in discover/onboarding for now):** home trades (`plumbers`, `electricians`, etc.) remain in the codebase for quote-request demos but are not part of Beauty & Wellness GTM.

### Example service lists (catalog templates, not subcategories)

**Massage & Bodywork** — Swedish, deep tissue, sports, couples, prenatal, hot stone, reflexology, …  

**Hair & Grooming** — haircut, barbering, braids, locs, styling, color, treatments, beard grooming, …  

Businesses pick services from templates and set price, duration, and availability. We do **not** need arbitrary depth (Category → Subcategory → Sub-subcategory).

---

## Three axes (do not conflate)

| Axis | Purpose | Examples |
| --- | --- | --- |
| **Discovery category** | What customers browse; SEO/filter | `hair-grooming`, `massage-bodywork` |
| **Business type** | Operating model (already in code) | `scheduled_appointment`, `quote_request` |
| **Booking delivery type** | How fulfillment works (new) | `appointment`, `class`, `session`, `experience`, `mobile_appointment` |

**Business type** stays coarse (appointment vs quote). Beauty & Wellness v1 is almost entirely **`scheduled_appointment`**. Quote flows stay for deferred home-services expansion.

**Booking delivery type** attaches to **services** (and drives calendar UX):

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

| Delivery type | Customer journey | Calendar shape |
| --- | --- | --- |
| `appointment` | Pick service → slot → confirm | 1:1 time slot |
| `class` | Pick class instance → seat/capacity | Fixed start, N seats |
| `session` | Pick trainer/coach → slot | 1:1 or small group |
| `experience` | Pick session (sauna block, cold plunge) | Capacity / turn |
| `mobile_appointment` | Address + travel buffer + slot | 1:1 + location |

v1 can ship with **`appointment` only** on `ServiceOffering`, adding enum values as yoga classes and sauna blocks need different UX.

---

## Business ↔ category (many-to-many)

Today: one `BusinessProfile.CategorySlug` drives discovery filter and onboarding defaults.

Target:

```
Business (tenant)
├── primaryCategorySlug     — ranking, SEO default, onboarding default
├── additionalCategorySlugs — discovery tags (spa also shows under massage, nails, skincare)
└── services[]              — each links to one category + optional template id
```

**Examples**

- Lagos day spa: primary `spa-relaxation`; additional `massage-bodywork`, `skincare-aesthetics`, `nails`.  
- Ottawa studio: primary `yoga-pilates`; additional `massage-bodywork`, `recovery-performance`.

Discovery should match if **any** listed category matches the filter (with primary used for sort tie-breakers and structured data).

---

## Market-specific prominence (same taxonomy)

Extend market catalog (or a sibling JSON file) with optional fields:

- `featuredCategorySlugs: string[]` — Explore row order  
- `onboardingCategorySlugs: string[]` — portal signup picker  
- `hiddenCategorySlugs: string[]` — e.g. hide trades in Ottawa/Lagos wellness launch  

Illustrative defaults (config only):

| Market | Emphasize |
| --- | --- |
| **Lagos** | hair & grooming, nails, spa, skincare, mobile-friendly services |
| **Ottawa** | massage, spa, skincare, fitness, yoga/Pilates, recovery |

---

## Mapping from current catalog (migration)

Current hardcoded slugs in `CategoryService` (`src/Adeni.Infrastructure/Catalog/CategoryService.cs`):

| Legacy slug | Proposed wellness slug | Notes |
| --- | --- | --- |
| `barbers` | `hair-grooming` | Merge barber + salon browse |
| `hair-salons` | `hair-grooming` | Same |
| `nail-spa` | `nails` (+ optional `spa-relaxation`) | Split spa vs nails at service level |
| `makeup-brows` | `skincare-aesthetics` | Non-medical aesthetics |
| `plumbers`, `electricians`, `cleaning` | — | Keep for quote demos; **exclude** from wellness Explore |

Parent group slug today: `beauty` | `home-services`. Proposed umbrella group: **`beauty-wellness`** (consumer copy: “Beauty, Wellness & Self-Care”).

---

## Code touchpoints (implementation checklist)

| Area | Today | Change |
| --- | --- | --- |
| Category API | Static list in `CategoryService` | Versioned catalog JSON or DB table; wellness slugs + `v1Enabled` flags |
| i18n | `packages/shared/src/i18n/messages/*` | Labels for new slugs + group |
| Workflows | `packages/shared/src/data/category-workflows.json` | Per-category defaults; drop quote defaults from v1 onboarding picker |
| Tenant profile | Single `CategorySlug` | `PrimaryCategorySlug` + `BusinessCategory` join table |
| Services | `ServiceOffering` name/price/duration only | Optional `categorySlug`, `catalogServiceId`, `bookingDeliveryType` |
| Discover | Lists all API categories | Filter by market `featuredCategorySlugs`; hide deferred/regulated |
| Onboarding | Picks one category | Primary + optional additional; service templates by category |
| SEO | Category in URL query | Stable slugs; primary category in JSON-LD |

**Do not** revive the old multi-industry capability matrix. Keep **business type + small capability list** (`category-workflows.json`) for portal feature flags.

---

## Phased rollout

1. **Docs + config-only** — This taxonomy, market featured lists, discover filters behind env flag; legacy slugs alias to new slugs in API.  
2. **Service catalog** — Shared JSON of template services per category; portal “add from catalog.”  
3. **Multi-category businesses** — Migration + discovery filter update.  
4. **Booking delivery types** — Enum + class/capacity when yoga/fitness supply requires it.  
5. **Regulated categories** — Separate compliance track before enabling 🔴 slugs.

---

## Positioning reminder

External copy should say **beauty & wellness** (or **self-care**), not “wellness app only,” if hair, nails, and aesthetics are in scope — otherwise we shrink TAM and mis-set customer expectations.
