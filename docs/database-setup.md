# Database setup

Docker is **optional**. It is only a convenience for running PostgreSQL (and Redis) locally without installing them. The API also supports a local PostgreSQL install, or an in-memory database for tests.

See [caching-setup.md](./caching-setup.md) for Redis configuration.

## Option A — Docker (recommended)

```powershell
docker compose up -d
dotnet ef database update --project src/Adeni.Infrastructure --startup-project src/Adeni.Api
```

Connection string is preconfigured in `appsettings.Development.json`.

## Option B — Local PostgreSQL install

1. Install PostgreSQL 16+
2. Create database and user:

```sql
CREATE USER adeni WITH PASSWORD 'adeni_dev_password';
CREATE DATABASE adeni OWNER adeni;
```

3. Update `appsettings.Development.json` if your host/port differs
4. Run migrations:

```powershell
dotnet ef database update --project src/Adeni.Infrastructure --startup-project src/Adeni.Api
```

## Option C — No database (in-memory fallback)

When `ConnectionStrings:AdeniDb` is empty **and** environment is `Development` or `Testing`, the API uses EF Core InMemory automatically. Suitable for unit tests; not for production-like dev.

## Verify

```powershell
dotnet run --project src/Adeni.Api
curl http://localhost:5xxx/health
```

Expect `"database": "healthy"` when PostgreSQL is connected.

## Development sample data

When the API runs in **Development** with PostgreSQL connected, it auto-seeds sample businesses (idempotent — skips slugs that already exist). Each business gets the full service menu for its category, a cover photo, four gallery photos, and Mon–Sat 9:00–17:00 availability. Restarting the API fills in any catalog services or gallery photos that are still missing.

Seed matches the Beauty & Wellness wedge: **Lagos + Ottawa** and the seven enabled categories (`hair-grooming`, `nails`, `skincare-aesthetics`, `spa-relaxation`, `massage-bodywork`, `fitness`, `yoga-pilates`). Home services and non-launch cities are not seeded.

| Market | Total | Handcrafted | Generated bulk |
|--------|-------|-------------|----------------|
| `lagos` | 1,400 | 7 | 1,393 |
| `ottawa` | 602 | 7 | 595 |

**2,002 businesses**, spread evenly across the seven categories. Generated slugs use `{market}-seed-{category}-{####}` (e.g. `lagos-seed-hair-grooming-0001`). `lekki-cuts` is the local dev-owner business. Restart the API to append any new slugs without resetting the DB. The first seed of this set can take a minute or two.

To re-seed from scratch, truncate tenant data (keep `catalog.markets` and `__EFMigrationsHistory`) and restart the API. The seeder will not replace rows whose slugs already exist.

## Optional dev UIs

Postgres and Redis do **not** include a web UI by default. Start Adminer and RedisInsight with:

```powershell
docker compose --profile ui up -d
```

| Tool | URL | Login |
|------|-----|-------|
| **Adminer** (PostgreSQL) | http://localhost:8080 | System: **PostgreSQL**, Server: **`postgres`** (not `localhost` or `db`), User: **`adeni`**, Password: **`adeni_dev_password`**, Database: **`adeni`** |
| **RedisInsight** (Redis) | http://localhost:5540 | Pre-configured as **adeni-redis** — or add manually with Host: **`redis`** (not `127.0.0.1`) |

### Why not `localhost` or `127.0.0.1`?

Adminer and RedisInsight run **inside Docker**. From inside a container, `localhost` means that container itself — not your machine and not the Postgres/Redis containers. Use the **Docker Compose service names**: `postgres` and `redis`.

If you already tried wrong settings, recreate the UI containers:

```powershell
docker compose --profile ui down
docker compose --profile ui up -d
```

Alternative desktop tools: [DBeaver](https://dbeaver.io/) or [Azure Data Studio](https://azure.microsoft.com/products/data-studio) for Postgres (connect to `localhost:5432`); [Another Redis Desktop Manager](https://github.com/qishibo/AnotherRedisDesktopManager) for Redis (connect to `localhost:6379`).
