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
- [x] Rewrite discovery raw SQL for T-SQL
- [x] Archive Postgres EF migrations to [adeni-legacy-db](https://github.com/acethsol/adeni-legacy-db)
- [x] Remove in-process `SqlSchemaApplicator` (not part of SSDT)
- [x] Update docs + CI (tests use InMemory)
- [ ] SqlPackage publish step in CI against a service container — follow-up
- [ ] Production Azure SQL provisioning — follow-up

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
| EF `AdeniDbContext` | Mapping + queries only |
| Development API | Sample seed only (`DevelopmentDataSeeder`) |

## 5. Legacy

Postgres EF migrations, early `001_Initial.sql` applicator experiment, and SchemaGen: [adeni-legacy-db](https://github.com/acethsol/adeni-legacy-db) — see [legacy-postgres-archive.md](../legacy-postgres-archive.md).
