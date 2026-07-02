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

When the API runs in **Development** with PostgreSQL connected, it auto-seeds six verified businesses (idempotent — skips if `lekki-cuts` already exists). Each has one service and Mon–Sat 9:00–17:00 availability.

| Slug | Market | City |
|------|--------|------|
| `lekki-cuts` | lagos | Lagos |
| `abuja-glow-salon` | abuja | Abuja |
| `ottawa-capitol-cuts` | ottawa | Ottawa |
| `toronto-annex-salon` | toronto | Toronto |
| `houston-montrose-barber` | houston | Houston |
| `dallas-deep-ellum-cuts` | dallas | Dallas |

Browse by market: `http://localhost:3000/?market=abuja` (sets cookie), then open `/discover`. Or use `NEXT_PUBLIC_ADENI_MARKET=toronto` in web env.

To re-seed from scratch, delete rows or drop the database and restart the API after migrations.

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
