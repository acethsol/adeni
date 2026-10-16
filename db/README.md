# Adeni.Database (SQL Server Database Project)

Schema is owned by **`Adeni.Database/`** — a [Microsoft.Build.Sql](https://learn.microsoft.com/sql/tools/sql-database-projects/sql-database-projects) project. EF Core maps entities only; do **not** add EF migrations.

| Path | Role |
|------|------|
| `Adeni.Database/**/*.sql` (tables, schemas) | Declarative model → `.dacpac` |
| `Adeni.Database/PreDeployment/` | Runs **before** SqlPackage applies the model |
| `Adeni.Database/PostDeployment/` | Runs **after** the model (grants, reference SQL) |
| `../scripts/publish-db.ps1` | Build + SqlPackage publish |
| `scripts/tenant-billing-columns.sql` | Idempotent billing columns on **existing** DBs |
| `../scripts/apply-tenant-billing-columns.ps1` | Run that script via sqlcmd (dev connection default) |

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

### Tenant billing columns (PR3)

After pulling billing work, either:

```powershell
./scripts/publish-db.ps1          # full model (includes tenancy.tenants billing fields)
# or, billing patch only on an existing DB:
./scripts/apply-tenant-billing-columns.ps1
```

Both are safe to re-run; `tenant-billing-columns.sql` uses `COL_LENGTH` guards.

### Unified floor board (queue + walk-in policy)

```powershell
./scripts/publish-db.ps1
# or patch only:
sqlcmd ... -i db/scripts/unified-floor-board.sql
```

## Visual Studio: “Target framework not installed”

`Adeni.Database` is an **SDK-style** SQL project (`Microsoft.Build.Sql`). Classic SSDT treats `.sqlproj` as .NET Framework and shows that dialog.

**Do not** choose “Update the target to .NET Framework 4.8” — that breaks CLI/`publish-db.ps1` builds.

| Option | What to do |
|--------|------------|
| **Quick** | Choose **Do not load this project**. Edit `.sql` in the repo; build/publish with `./scripts/publish-db.ps1` or `dotnet build db/Adeni.Database`. |
| **Full VS support** | VS 2022 **17.12+**, install workload component **SQL Server Data Tools SDK-style (Preview)** *and* **.NET SDK**. Prefer a separate VS install — side-by-side with classic SSDT is not supported. |
| **Editor** | [SQL Database Projects](https://marketplace.visualstudio.com/items?itemName=ms-mssql.sql-database-projects-vscode) in VS Code / Cursor. |

Docs: [SDK-style SSDT (preview)](https://learn.microsoft.com/sql/ssdt/sql-server-data-tools-sdk-style).

## Dev seed vs post-deploy

Bulk Lagos/Ottawa sample businesses stay in `DevelopmentDataSeeder` (API, Development only). Put stable reference SQL (permissions, lookup rows shared by all environments) in **PostDeployment**.
