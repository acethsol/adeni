# Adeni Backend

Trusted local services marketplace API — **.NET 10**, **DDD/Clean Architecture**, **SOC 2 controls built in from Sprint 0**.

## Quick start

```powershell
cd C:\DEV\Aceth\adeni
docker compose up -d                              # PostgreSQL + Redis
docker compose --profile ui up -d                 # optional: Adminer + RedisInsight
dotnet restore Adeni.slnx
dotnet ef database update --project src/Adeni.Infrastructure --startup-project src/Adeni.Api
dotnet test Adeni.slnx -c Release
dotnet run --project src/Adeni.Api --launch-profile http
```

| Endpoint | URL |
|----------|-----|
| Health | http://localhost:5169/health |
| API docs (dev) | http://localhost:5169/scalar/v1 |
| PostgreSQL UI | http://localhost:8080 (Adminer, `--profile ui`) |
| Redis UI | http://localhost:5540 (RedisInsight, `--profile ui`) |

See [docs/database-setup.md](docs/database-setup.md) and [docs/caching-setup.md](docs/caching-setup.md).

## Solution structure

```
src/
├── Adeni.Domain/           # Entities, Result<T>, value objects
├── Adeni.Application/      # Abstractions, caching, catalog, PiiMasker
├── Adeni.Infrastructure/   # EF Core, Redis, Auth0 JWT, Key Vault, audit
└── Adeni.Api/              # Middleware pipeline, controllers, OpenAPI/Scalar
tests/                      # 88 unit/integration tests
```

## Current features

| Feature | Implementation |
|---------|----------------|
| Auth0 JWT | `AddAdeniAuth()` — RS256, audience/issuer validation |
| Auth sync | `POST /api/v1/auth/sync` — upsert customer or business user |
| Admin verification | `GET /admin/businesses/pending`, approve/reject with audit |
| Categories | `GET /api/v1/categories` — Redis-cached beauty vertical |
| Redis caching | `ICacheService`, slot locks, health check |
| Admin MFA policy | `AdminMfaPolicy` requires `amr: mfa` claim |
| PostgreSQL + EF Core | `AdeniDbContext`, schemas `identity`, `tenancy`, `admin` |
| Dev API docs | Scalar UI + OpenAPI 3.1 (Development only) |
| In-memory fallback | Auto when Postgres/Redis not configured in Dev/Testing |

## Configuration

**Development** (`appsettings.Development.json`):

```json
{
  "ConnectionStrings": { "AdeniDb": "Host=localhost;..." },
  "Redis": { "ConnectionString": "localhost:6379" },
  "Auth0": { "Enabled": false }
}
```

Set `Auth0:Enabled=true` when testing Auth0 integration. See [docs/auth0-setup.md](docs/auth0-setup.md).

## Sprints

| Sprint | Focus | Status |
|--------|-------|--------|
| **0** | Foundation, Redis, OpenAPI, CI, dev UIs | Done |
| **1** | Business onboarding (register → verify → approve) | **Current** |
| **2** | Discovery + public business profiles | Planned |
| **3** | Auth0 + Flutter shell | Planned |
| **4** | Booking + Redis slot locks | Planned |

Details: [docs/sprints.md](docs/sprints.md)

## Compliance docs (Confluence)

- [SOC 2 Compliance Framework](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170)
- [MFA Enforcement Runbook](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247205)
- [Privacy Policy Legal Review Checklist](https://aceth.atlassian.net/wiki/spaces/SD/pages/26738699)

## Remote

**GitHub:** [github.com/acethsol/adeni](https://github.com/acethsol/adeni)

```powershell
git push origin main
```
