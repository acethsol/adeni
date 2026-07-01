# Sprint plan

## Sprint 0 — Foundation & dev tooling ✅

- [x] DDD scaffold, SOC 2 Sprint 0 controls, 88 tests
- [x] Docker Postgres + Redis
- [x] Redis caching (`ICacheService`, categories endpoint)
- [x] OpenAPI + Scalar (Development)
- [x] GitHub Actions CI (test + vulnerability scan)
- [x] Optional dev UIs (Adminer, RedisInsight)
- [ ] Commit + push to GitHub

## Sprint 1 — Business onboarding (supply side)

**Goal:** A business can register, submit verification, and be approved by admin.

| Task | Endpoint / artifact |
|------|---------------------|
| Tenant registration | `POST /api/v1/tenant/register` |
| Verification submit | `POST /api/v1/tenant/verification` |
| Business profile read/update | `GET/PATCH /api/v1/tenant/profile` |
| Schema | `BusinessProfile`, `VerificationDocument` entities + migration |
| Tests | Service + integration tests for full approve flow |

**Done when:** Register → submit docs → admin approves → tenant status `Active`.

## Sprint 2 — Discovery (demand side)

**Goal:** Customers can find approved businesses near them.

| Task | Endpoint / artifact |
|------|---------------------|
| Discovery search | `GET /api/v1/discovery?lat=&lng=&category=&page=` |
| Public profile | `GET /api/v1/businesses/{slug}` |
| Caching | `discovery:*` and `tenant:{id}:profile` keys |

**Done when:** Approved business appears in geo/category search with cached response.

## Sprint 3 — Auth0 + Flutter shell

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
