# Spec: SQL Server Database Project (EF as ORM only)

| Field | Value |
|-------|-------|
| **Sprint** | Platform (cross-cutting) |
| **Status** | Done (local); CI SqlPackage step optional follow-up |
| **Created** | 2026-10-09 |

## 1. Goal

Replace PostgreSQL with **SQL Server**, own schema in a **Microsoft.Build.Sql Database Project**, deploy with **SqlPackage** (pre/post-deployment scripts), and use **EF Core only as the ORM**.

## 2. In scope

- [x] Docker SQL Server (Azure SQL Edge for ARM hosts)
- [x] `UseSqlServer` + connection string
- [x] `db/Adeni.Database` `.sqlproj` + object scripts
- [x] PreDeployment / PostDeployment scripts
- [x] `scripts/publish-db.ps1` (SqlPackage)
- [x] Discovery search as `tenancy.DiscoverySearch` stored procedure (no embedded SQL in app)
- [x] Archive Postgres EF migrations to [adeni-legacy-db](https://github.com/acethsol/adeni-legacy-db)
- [x] Remove in-process `SqlSchemaApplicator` (not part of SSDT)
- [x] Update docs + CI (tests use InMemory)
- [x] Unit tests for `StoredProcedureExecutor` + `DiscoverySearchExecutor.MapRow` (InMemory/fake ADO; always in CI)
- [x] Opt-in SQL Server discovery tests (`Category=SqlServer`, `ADENI_SQLSERVER_TESTS=1`)
- [ ] SqlPackage publish step in CI against a service container — follow-up
- [ ] Production Azure SQL provisioning — follow-up
- [ ] Run `Category=SqlServer` tests in CI after SqlPackage — follow-up

## 3. Out of scope

- Postgres dual-write / dual-run
- Keeping Adminer for SQL Server (use Azure Data Studio / SSMS)
- Changing Redis
- Homemade versioned script runners in the API

## 4. Deploy model

| Layer | Owns |
|-------|------|
| `db/Adeni.Database` | Declarative tables/schemas/indexes |
| PreDeployment | Before model apply |
| SqlPackage Publish | Diff + apply model |
| PostDeployment | After model apply |
| EF `AdeniDbContext` | Mapping + LINQ queries only |
| Stored procedures | Heavy/set-based SQL (e.g. `tenancy.DiscoverySearch`) — CTEs OK inside procs |
| SP call sites | `StoredProcedureExecutor` (generic ADO) + thin domain wrappers (e.g. `DiscoverySearchExecutor`) |
| Development API | Sample seed only (`DevelopmentDataSeeder`) |

### Stored procedure header convention

Every proc under `db/Adeni.Database/**/StoredProcedures/` starts with a block comment covering:

1. **Procedure** — schema-qualified name  
2. **Purpose** — what/why  
3. **Caller** — C# executor type  
4. **Parameters** — each input/OUTPUT  
5. **Notes** — non-obvious SQL choices  
6. **Sample call** — runnable `EXEC` + expected result columns  

Template: [`db/Adeni.Database/_templates/StoredProcedure.sql`](../../db/Adeni.Database/_templates/StoredProcedure.sql) (not deployed — `_templates` is documentation only). Reference: [`tenancy.DiscoverySearch`](../../db/Adeni.Database/tenancy/StoredProcedures/DiscoverySearch.sql).

### Testing stored procedures

| Suite | When it runs | What it covers |
|-------|----------------|----------------|
| `StoredProcedureExecutorTests` | Always (CI) | ADO binding, OUTPUT, open/close connection |
| `DiscoverySearchExecutorTests` | Always (CI) | Result row mapping |
| `DiscoverySearchSqlServerTests` | Opt-in | Real `tenancy.DiscoverySearch` via `DiscoveryService` |

```powershell
pwsh scripts/publish-db.ps1
$env:ADENI_SQLSERVER_TESTS = "1"
dotnet test tests/Adeni.Infrastructure.Tests --filter "Category=SqlServer"
```

## 5. Legacy

Postgres EF migrations, early `001_Initial.sql` applicator experiment, and SchemaGen: [adeni-legacy-db](https://github.com/acethsol/adeni-legacy-db) — see [legacy-postgres-archive.md](../legacy-postgres-archive.md).
