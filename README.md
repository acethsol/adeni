# Adeni

Trusted local services marketplace — **.NET 10 API** + **Next.js web** + **Expo mobile**, SOC 2 controls from Sprint 0.

**Strategy:** [docs/product-strategy.md](docs/product-strategy.md) — positioning, revenue model, GTM wedge, AI roadmap.

## Quick start

```powershell
cd C:\DEV\Aceth\adeni
docker compose up -d                              # PostgreSQL + Redis
docker compose --profile ui up -d                 # optional: Adminer + RedisInsight
dotnet ef database update --project src/Adeni.Infrastructure --startup-project src/Adeni.Api
dotnet test Adeni.slnx -c Release
dotnet run --project src/Adeni.Api --launch-profile http

# Frontend (separate terminal)
npm install
npm run dev:web                                   # http://localhost:3000
```

| Service | URL |
|---------|-----|
| API | http://localhost:5169 |
| API docs (dev) | http://localhost:5169/scalar/v1 |
| Web app | http://localhost:3000 |
| PostgreSQL UI | http://localhost:8080 (Adminer, `--profile ui`) |
| Redis UI | http://localhost:5540 (RedisInsight, `--profile ui`) |

See [docs/database-setup.md](docs/database-setup.md), [docs/caching-setup.md](docs/caching-setup.md), [docs/frontend.md](docs/frontend.md), [docs/architecture.md](docs/architecture.md), [docs/observability.md](docs/observability.md).

## Repository structure

```
src/                     .NET backend — **modular monolith** (see docs/architecture.md)
apps/web/                Next.js — public, business, admin
apps/mobile/             Expo — unified customer + business app
packages/api-client/     Typed API client (shared)
packages/shared/         Zod schemas, roles
The Flutter prototype was retired in July 2026 (ADR-010) and is not kept in this repository.
tests/                   Backend unit/integration tests
```

## Current features (API)

| Feature | Implementation |
|---------|----------------|
| Auth0 JWT | `AddAdeniAuth()` — RS256, audience/issuer validation |
| Auth sync | `POST /api/v1/auth/sync`, `GET /api/v1/auth/me` |
| Admin verification | Pending queue, approve/reject with audit |
| Categories | `GET /api/v1/categories` — Redis-cached |
| Discovery | `GET /api/v1/discovery` — geo search (Verified only) |
| Public profiles | `GET /api/v1/businesses/{slug}` — masked phone |
| Redis caching | `ICacheService`, slot locks, health check |
| Booking | Services CRUD, weekly availability, slot search, `POST /api/v1/bookings` |
| CORS | Next.js web origins (`localhost:3000`) |

## Sprints

| Sprint | Focus | Status |
|--------|-------|--------|
| **0** | Foundation, Redis, OpenAPI, CI, dev UIs | Done |
| **1** | Business onboarding | Done |
| **2** | Discovery + public profiles | Done |
| **3** | Auth0 + client shell (backend); frontend pivot | Done |
| **13** | Reviews & ratings | Done |
| **14** | UX polish & guardrails | Done |
| **15–20** | Booking v2 → SaaS → Paystack → Messaging → Trust → AI/deploy | Planned |

Details: [docs/sprints.md](docs/sprints.md) · Strategy: [docs/product-strategy.md](docs/product-strategy.md)

## Compliance docs (Confluence)

- [Adeni Product Bible](https://aceth.atlassian.net/wiki/spaces/SD/pages/26279937)
- [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649)
- [Product strategy & revenue model](https://aceth.atlassian.net/wiki/spaces/SD/pages/41091073)
- [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065)
- [Observability v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/27230210)
- [SOC 2 Compliance Framework](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170)

## Remote

**GitHub:** [github.com/acethsol/adeni](https://github.com/acethsol/adeni)
