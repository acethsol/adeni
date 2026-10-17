# Adeni

Trusted local services marketplace — **.NET 10 API** + **Angular web clients** (discover / portal / admin), SOC 2 controls from Sprint 0.

**Strategy:** [docs/product-strategy.md](docs/product-strategy.md) — positioning, revenue model, GTM wedge, AI roadmap.

## Quick start

```powershell
cd C:\DEV\Aceth\adeni
docker compose up -d                              # SQL Server (Azure SQL Edge) + Redis
docker compose --profile ui up -d                 # optional: RedisInsight
./scripts/publish-db.ps1 -CreateNewDatabase       # SqlPackage: pre → schema → post
dotnet test Adeni.slnx -c Release
dotnet run --project src/Adeni.Api --launch-profile http   # Development seed only

# Frontend (separate terminal)
npm install
npm run dev:discover                              # http://localhost:5190 (public)
npm run dev:portal                                # http://localhost:5173 (business)
```

| Service | URL |
|---------|-----|
| API | http://localhost:5169 |
| API docs (dev) | http://localhost:5169/scalar/v1 |
| Discover (Angular) | http://localhost:5190 |
| Business portal | http://localhost:5173 |
| Admin portal | http://localhost:5180 |
| SQL Server | `localhost,1433` (sa / see appsettings.Development.json) |
| Redis UI | http://localhost:5540 (RedisInsight, `--profile ui`) |

See [docs/database-setup.md](docs/database-setup.md), [docs/caching-setup.md](docs/caching-setup.md), [docs/frontend.md](docs/frontend.md), [docs/architecture.md](docs/architecture.md), [docs/observability.md](docs/observability.md), [docs/ci.md](docs/ci.md).

## Repository structure

```
src/                     .NET backend — **modular monolith** (see docs/architecture.md)
db/Adeni.Database/       SQL Server Database Project (.sqlproj + SqlPackage)
apps/discover/           Angular SSR — public discovery & booking
apps/portal/             Angular — business portal
apps/admin/              Angular — admin portal
packages/api-client/     Typed API client (shared)
packages/shared/         Zod schemas, roles, wellness catalog
packages/ui/             Shared Angular UI for portal + admin

apps/mobile/             Flutter — business mobile skeleton (owner / employee / front desk)
tests/                   Backend unit/integration tests
```

**Archives (separate repos):** [Next.js + Expo](docs/legacy-clients-archive.md) · [PostgreSQL EF / compose](docs/legacy-postgres-archive.md)

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
| CORS | Angular dev origins (`5173`, `5180`, `5190`) |

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
