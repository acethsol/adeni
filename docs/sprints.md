# Sprint plan

## Sprint 0 — Foundation & dev tooling ✅

- [x] DDD scaffold, SOC 2 Sprint 0 controls, 92 tests
- [x] Docker Postgres + Redis
- [x] Redis caching (`ICacheService`, categories endpoint)
- [x] OpenAPI + Scalar (Development)
- [x] GitHub Actions CI (test + vulnerability scan)
- [x] Optional dev UIs (Adminer, RedisInsight)
- [x] Commit + push to GitHub

## Sprint 1 — Business onboarding (supply side) ✅

**Done when:** Register → submit docs → admin approves → tenant status `Verified`.

## Sprint 2 — Discovery (demand side) ✅

**Goal:** Customers can find approved businesses near them.

| Task | Endpoint / artifact |
|------|---------------------|
| Discovery search | `GET /api/v1/discovery?lat=&lng=&category=&page=` |
| Public profile | `GET /api/v1/businesses/{slug}` |
| Caching | `discovery:*` and `tenant:{id}:profile` keys |

**Done when:** Approved business appears in geo/category search with cached response.

## Sprint 3 — Auth0 + Flutter shell ← **current**

**Goal:** Real authentication end-to-end on mobile.

| Task | Artifact |
|------|----------|
| Auth0 tenant + apps | Staging config, MFA Login Action |
| Enable JWT in dev/staging | `Auth0:Enabled=true` |
| Flutter project | Auth0 login, auth sync, categories list |

## Sprint 4 — Booking

**Goal:** First bookable appointment with concurrency safety.

| Task | Endpoint / artifact |
|------|---------------------|
| Services CRUD | Business manages services |
| Availability | Business hours + slots |
| Booking create | `POST /api/v1/bookings` |
| Slot locks | Redis `slot-lock:*` via `IDistributedLockProvider` |
