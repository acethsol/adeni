# Adeni.Database (SQL Server Database Project)

Schema is owned by **`Adeni.Database/`** — a [Microsoft.Build.Sql](https://learn.microsoft.com/sql/tools/sql-database-projects/sql-database-projects) project. EF Core maps entities only; do **not** add EF migrations.

| Path | Role |
|------|------|
| `Adeni.Database/**/*.sql` (tables, schemas) | Declarative model → `.dacpac` |
| `Adeni.Database/PreDeployment/` | Runs **before** SqlPackage applies the model |
| `Adeni.Database/PostDeployment/` | Runs **after** the model (grants, reference SQL) |
| `../scripts/publish-db.ps1` | Build + SqlPackage publish |

## Local publish

```powershell
docker compose up -d
./scripts/publish-db.ps1 -CreateNewDatabase
dotnet run --project src/Adeni.Api --launch-profile http   # seeds sample data only
```

SqlPackage is installed as a local .NET tool (`dotnet-tools.json`).

## Changing schema

1. Edit object scripts under `Adeni.Database/` (or use the SQL Database Projects extension)
2. `./scripts/publish-db.ps1`
3. Align EF entity configuration if the contract changed

## Dev seed vs post-deploy

Bulk Lagos/Ottawa sample businesses stay in `DevelopmentDataSeeder` (API, Development only). Put stable reference SQL (permissions, lookup rows shared by all environments) in **PostDeployment**.
