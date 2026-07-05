# Sprint plan

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

## Next up

1. Staging + Auth0 E2E
2. Obs 1 — App Insights on API
