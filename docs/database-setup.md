# Database setup

Adeni uses **SQL Server**. Schema is a git-managed **SQL Server Database Project** at [`db/Adeni.Database`](../db/README.md). Deploy with **SqlPackage** (pre/post-deployment scripts included). EF Core is the ORM only — no EF migrations.

Docker is **optional** for local SQL Server (Azure SQL Edge) and Redis. See [caching-setup.md](./caching-setup.md) for Redis.

## CI

GitHub Actions job `sqlserver` (see [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)) publishes the dacpac with SqlPackage against SQL Server 2022, then runs `Category=SqlServer` tests. Local Docker Compose still uses Azure SQL Edge for ARM hosts.

## Option A — Docker (recommended)

```powershell
docker compose up -d
./scripts/publish-db.ps1 -CreateNewDatabase
dotnet run --project src/Adeni.Api --launch-profile http
```

| Step | Owns |
|------|------|
| `publish-db.ps1` | Build `.dacpac`, run pre-deploy → schema sync → post-deploy |
| API (Development) | Idempotent sample seed only |

Connection string is preconfigured in `appsettings.Development.json`:

`Server=localhost,1433;Database=adeni;User Id=sa;Password=Adeni_Dev_Passw0rd!;TrustServerCertificate=True;Encrypt=False`

## Option B — Local / Azure SQL

1. Point `ConnectionStrings:AdeniDb` at the server
2. `./scripts/publish-db.ps1 -CreateNewDatabase` (omit `-CreateNewDatabase` if the DB already exists)
3. Start the API for Development seed (optional)

## Option C — No database (in-memory fallback)

When `ConnectionStrings:AdeniDb` is empty **and** environment is `Development` or `Testing`, the API uses EF Core InMemory automatically. Suitable for unit tests; not for production-like dev.

## Verify

```powershell
dotnet run --project src/Adeni.Api --launch-profile http
curl http://localhost:5169/health
```

Expect `"database": "healthy"` when SQL Server is connected and the project has been published.

## Development sample data

When the API runs in **Development** with SQL Server connected, it auto-seeds sample businesses (idempotent — skips slugs that already exist). Schema must already exist via SqlPackage.

Seed matches the Beauty & Wellness wedge: **Lagos + Ottawa** and the enabled wellness categories. See prior seed counts in git history / seeder constants if regenerating markets.

To re-seed from scratch, drop/recreate the `adeni` database, re-run `./scripts/publish-db.ps1 -CreateNewDatabase`, then restart the API.

## Optional Redis UI

```powershell
docker compose --profile ui up -d
```

| Tool | URL | Notes |
|------|-----|-------|
| **RedisInsight** | http://localhost:5540 | Host inside Docker network: **`redis`** |
| **Azure Data Studio / SSMS** | `localhost,1433` | sa / `Adeni_Dev_Passw0rd!`, database `adeni` |

## Legacy PostgreSQL

Postgres EF migrations and early cutover helpers live in a separate archive: [legacy-postgres-archive.md](./legacy-postgres-archive.md) → https://github.com/acethsol/adeni-legacy-db
