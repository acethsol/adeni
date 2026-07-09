# Sprint plan

## Status overview (July 2026)

| Sprint | Name | Status |
|--------|------|--------|
| 0 | Foundation & dev tooling | ✅ Done |
| 1 | Business onboarding | ✅ Done |
| 2 | Discovery API | ✅ Done |
| 3 | Auth0 + client foundation | ✅ Done |
| 3b | Web public shell | ✅ Done |
| 4 | Booking | ✅ Done |
| 5 | Expo mobile loop | ✅ Done |
| 6 | Business portal (web) | ✅ Done |
| 7 | Customer bookings | ✅ Done |
| 8 | MVP gap closure | ✅ Done |
| 9 | Mobile business onboarding | ✅ Done |
| 10 | Design system & caching | ✅ Done |
| 11 | Discovery UX | ✅ Done |
| **12** | **Media & tenant hardening** | ✅ Done |
| **13** | **Reviews & ratings** | **In progress** |
| 14 | Deployment, AI & observability | Planned |
| — | Sprint 11d LLM agent | → Sprint 14 |
| — | Staging deploy + Auth0 E2E | → Sprint 14 |
| — | App Insights (Obs 1) | → Sprint 14 |

**Not yet tackled:** Sprint 14 (staging, LLM agent, App Insights).

---
## Sprint 0 — Foundation & dev tooling ✅

- [x] DDD scaffold, SOC 2 Sprint 0 controls
- [x] Docker Postgres + Redis, OpenAPI/Scalar, CI, dev UIs

## Sprint 1 — Business onboarding ✅

Register → submit docs → admin approves → tenant `Verified`.

## Sprint 2 — Discovery ✅

- `GET /api/v1/discovery`, `GET /api/v1/businesses/{slug}`
- Redis keys: `discovery:*`, `tenant:{id}:profile`

## Sprint 3 — Auth0 + client foundation ✅

**Backend (done):**

- [x] `GET /api/v1/auth/me`
- [x] CORS for web clients
- [x] `appsettings.Staging.json`

**Frontend pivot (July 2026 — ADR-010):**

- [x] Confluence updated ([Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065))
- [x] Flutter archived → `mobile/_archive/adeni_app_flutter`
- [x] Monorepo: `apps/web` (Next.js), `apps/mobile` (Expo), `packages/*`

## Sprint 3b — Web public shell ✅

**Goal:** SEO-ready discovery landing wired to existing API; market-aware, industry-neutral shell.

| Task | Status |
|------|--------|
| Public landing | ✅ `/` — categories from API, runtime market context |
| Business profile SSR | ✅ `/businesses/[slug]` |
| Discover page | ✅ `/discover` → discovery API + category filters |
| Dynamic market/categories | ✅ `packages/shared` market config; generic category API |
| Auth0 Next.js SDK | ✅ Login + role-gated `/business` and `/admin` |

## Sprint 4 — Booking ✅

Services CRUD, availability, `POST /api/v1/bookings`, Redis slot locks, geo markets, multi-branch locations, web booking UI.

## Sprint 5 — Expo mobile loop ✅

Unified app: browse, book, business booking management.

| Task | Status |
|------|--------|
| API client + market/geo context | ✅ |
| Home tab (categories, market copy) | ✅ |
| Discover tab (list + category filters) | ✅ |
| Business profile screen | ✅ |
| Customer booking flow | ✅ |
| Auth0 Native login | ✅ |
| Business booking management | ✅ |

## Sprint 6 — Business portal (web) ✅

Bring the web business portal to parity with mobile — profile, services, availability, booking inbox.

| Task | Status |
|------|--------|
| Portal shell + dev business auth | ✅ |
| Dashboard overview | ✅ |
| Booking inbox (accept/reject) | ✅ |
| Profile view + edit | ✅ |
| Services list | ✅ |
| Services CRUD UI | ✅ |
| Weekly availability UI | ✅ |
| Verification submission | ✅ |
| Multi-location management | ✅ |

## Sprint 7 — Customer bookings ✅

Let customers view booking history on web and mobile.

| Task | Status |
|------|--------|
| `GET /api/v1/bookings` (customer list) | ✅ |
| Shared schema + API client | ✅ |
| Web `/my-bookings` page | ✅ |
| Mobile my bookings screen | ✅ |

## Sprint 8 — MVP gap closure ✅

Close remaining product gaps before staging deploy.

| Task | Status |
|------|--------|
| Admin approve/reject UI | ✅ |
| Business self-signup web flow | ✅ |
| Customer cancel booking | ✅ |
| SOC2-09 customer export/delete admin | ✅ |

## Sprint 9 — Mobile business onboarding ✅

Business owners can register and manage verification from the Expo app.

| Task | Status |
|------|--------|
| Business tenant resolution via `getBusinessContext` | ✅ |
| Register business screen | ✅ |
| Profile edit + verification submit | ✅ |
| Account tab business navigation | ✅ |
| Booking inbox uses resolved tenant | ✅ |

## Sprint 10 — Design system, polish & client caching ✅

Shared tokens, UI primitives, empty/loading states, and TanStack Query on web + mobile.

| Task | Status |
|------|--------|
| `@adeni/shared` design tokens + query keys | ✅ |
| Web `components/ui/*` + Tailwind theme | ✅ |
| Mobile `components/ui/*` + shared theme | ✅ |
| Public shells & key pages refactored | ✅ |
| TanStack Query (categories, discovery, bookings) | ✅ |
| Docs: [design-system.md](./design-system.md) | ✅ |

## Sprint 11 — Discovery UX ✅

Global search, visual discovery cards, fused Ask Adeni search, and profile heroes.

| Task | Status |
|------|--------|
| API `GET /discovery?q=` keyword filter | ✅ |
| Fused DiscoverySearch (web header + mobile home/discover) | ✅ |
| Category visuals + image business cards | ✅ |
| Ask Adeni rule-based intent parser | ✅ |
| Business profile hero image (category fallback) | ✅ |
| Business cover upload + blob storage | Deferred → [media-storage.md](./media-storage.md) |
| LLM agent (11d) | → Sprint 14 |

Confluence: [Sprint 11 — Discovery UX](https://aceth.atlassian.net/wiki/spaces/SD/pages/28540929)

## Sprint 12 — Media & tenant hardening ✅

| Task | Status |
|------|--------|
| **12a** `IFileStorage` port (Local + Azure Blob) | ✅ |
| **12b** `cover_image_key` + presigned upload URL API | ✅ |
| **12c** Business portal cover photo upload UI (web + mobile) | ✅ |
| **12e** Tenant isolation hardening (see below) | ✅ |

**Deferred to Sprint 14:** staging deploy, Auth0 E2E, LLM agent (11d), App Insights.

### 12e — Tenant isolation hardening

Foundation exists (middleware + EF global filters + audit). Sprint 12 closes gaps before staging.

| Task | Status |
|------|--------|
| Integration tests: tenant A cannot access tenant B bookings/services | ✅ |
| Integration tests: EF filter blocks cross-tenant reads when filter enabled | ✅ |
| Architecture test: every `ITenantEntity` has a global query filter | ✅ |
| Expand `AdeniDbContextTenantFilterTests` (bookings, services, locations) | ✅ |
| Document cross-tenant vs intentionally public routes | ✅ `docs/tenant-isolation.md` |
| Cache key convention: tenant-private keys must include `{tenantId}` | ✅ `docs/tenant-isolation.md` |

**Current model (no change):** `/api/v1/tenant/*` requires `X-Tenant-Id` → `TenantAccessMiddleware` → EF filter ON. Public discovery/customer routes intentionally cross-tenant. Admin routes role-gated, filter OFF.

See [media-storage.md](./media-storage.md).

Confluence: [Sprint 12 — Media & tenant hardening](https://aceth.atlassian.net/wiki/spaces/SD/pages/28540956)

## Sprint 13 — Reviews & ratings ✅

| Task | Status |
|------|--------|
| `reviews` schema + one-review-per-booking constraint | ✅ |
| `POST /bookings/{id}/review` + public list API | ✅ |
| `ratingAvg` / `reviewCount` on discovery + profile DTOs | ✅ |
| Customer review flow (web + mobile my-bookings) | ✅ |
| Star ratings on discovery cards + profile section | ✅ |
| Admin soft-hide + audit (`review.hidden`) | ✅ |
| Live E2E + integration test (completed booking → review → public ratings) | ✅ |

Confluence: [Sprint 13 — Reviews & ratings](https://aceth.atlassian.net/wiki/spaces/SD/pages/28672001)

## Sprint 14 — Deployment, AI & observability (planned)

| Task | Status |
|------|--------|
| Staging deploy + Auth0 E2E (was 12d) | Planned |
| Sprint 11d — LLM Ask Adeni agent | Planned |
| Obs 1 — App Insights on API | Planned |

## Next up

1. **Sprint 14** — staging, LLM agent, App Insights
