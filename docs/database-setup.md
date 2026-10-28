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

**Lekki Cuts staff + branches (local dev):** After pulling location-scope changes, restart the API once in Development — `DevelopmentDataSeeder` adds the Phase 2 branch (`lekki-cuts-phase2`), assigns demo staff per branch, and backfills any existing Lekki staff rows that still have null `BusinessLocationId` to the primary branch. Existing databases that skip the seeder can run [`db/scripts/staff-location-scope.sql`](../db/scripts/staff-location-scope.sql) (backfill to primary) and then restart the API for the full Lekki roster seed.

**Lekki Cuts demo volume (portal/admin grids):** the API seeder only creates a handful of rows. For ~30 staff per branch plus customers, bookings (past / today / upcoming), reviews, booking + profile message threads with unread, queue tickets and calendar blocks (leave, closed dates), run:

```powershell
./scripts/apply-lekki-cuts-demo-seed.ps1            # seed demo data + wellness consultation intake (idempotent)
./scripts/apply-lekki-cuts-demo-seed.ps1 -Cleanup   # remove only the rows these scripts created
```

Requires `sqlcmd` on PATH, the schema published (`./scripts/publish-db.ps1`) and the API started once in Development (so `lekki-cuts`, its branches, services and the `auth0|local-business` owner exist). It reads `ConnectionStrings:AdeniDb` from `appsettings.Development.json` (SQL login or `-E`); or pass `-ConnectionString`. Direct: `sqlcmd -S localhost,1433 -U sa -P <pwd> -C -d adeni -I -i db/scripts/seed-lekki-cuts-demo.sql` then `-i db/scripts/seed-lekki-cuts-intake-consultation.sql` (`-I` = QUOTED_IDENTIFIER ON, needed for filtered indexes). SQL: [`seed-lekki-cuts-demo.sql`](../db/scripts/seed-lekki-cuts-demo.sql), [`seed-lekki-cuts-demo-cleanup.sql`](../db/scripts/seed-lekki-cuts-demo-cleanup.sql), [`seed-lekki-cuts-intake-consultation.sql`](../db/scripts/seed-lekki-cuts-intake-consultation.sql), [`seed-lekki-cuts-intake-consultation-cleanup.sql`](../db/scripts/seed-lekki-cuts-intake-consultation-cleanup.sql). The intake script publishes **Wellness consultation** (mapped from a scanned spa form) and links it to **Barbering**, **Braids**, and **Locs**. Fake PII only (`@seed.lekki.local`, `auth0|dev-lekki-demo-NNN`). Bookings are relative to the run time, so re-run cleanup + seed to refresh "today/upcoming" dates.

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
