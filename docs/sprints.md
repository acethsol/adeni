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

## Sprint 5 — Expo mobile loop ← **next**

Unified app: browse, book, business booking management (after web booking works).
