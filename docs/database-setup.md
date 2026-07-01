# Database setup

Docker is not required but is the fastest path for local development.

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
