# Redis caching setup

Redis is required in staging and production (Azure Cache for Redis). Local development can use Redis, or fall back to in-memory cache automatically.

## Why Redis from day 1

| Use case | Key pattern | TTL |
|----------|-------------|-----|
| Tenant public profile | `tenant:{id}:profile` | 5 min |
| Discovery search results | `discovery:{lat}:{lng}:{cat}:{page}` | 2 min |
| Booking slot locks | `slot-lock:{tenant}:{start}:{service}` | 30 sec |
| Category tree | `categories:all` | 1 hour |

The API uses `ICacheService` (`IDistributedCache` under the hood) and `IDistributedLockProvider` for slot locking.

## Option A — Docker (optional convenience)

Docker is **not required** to run the API. It is only a quick way to spin up PostgreSQL and Redis locally without installing them.

```powershell
docker compose up -d
```

Then set in `appsettings.Development.json`:

```json
"Redis": {
  "ConnectionString": "localhost:6379"
}
```

## Option B — Local Redis install

Install Redis 7+ and set:

```json
"Redis": {
  "ConnectionString": "localhost:6379"
}
```

## Option C — No Redis (in-memory fallback)

When `Redis:ConnectionString` is empty **and** environment is `Development` or `Testing`, the API uses `DistributedMemoryCache` automatically. `/health` reports `"cache": "memory_fallback"`.

Slot locks become no-ops in this mode — fine for unit tests, not for multi-instance booking concurrency.

## Verify

```powershell
dotnet run --project src/Adeni.Api
curl http://localhost:5xxx/health
curl http://localhost:5xxx/api/v1/categories
```

Expect `"cache": "healthy"` when Redis is connected, or `"memory_fallback"` when using the dev fallback.
